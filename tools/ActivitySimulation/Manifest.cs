using System.Text.Json;
using GMSoft.Data.Context;
using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace ActivitySimulation;

public sealed record SeedRow(string Table, Guid Id, string Snapshot);
public sealed record CustomerChange(Guid Id, DateTime? Before, DateTime After);
public sealed record Manifest(int Version, string Database, Guid Run, Guid[] Customers,
    List<SeedRow> Rows, List<CustomerChange> Changes)
{
    public static string Quote(string name) => "\"" + name.Replace("\"", "\"\"") + "\"";

    public static async Task<string?> Snapshot(AppDbContext db, string table, Guid id)
    {
        // Names must originate in the compiled EF model, never arbitrary manifest SQL.
        if (!db.Model.GetEntityTypes().Any(t => t.GetTableName() == table))
            throw new InvalidOperationException("Tabla desconocida en manifiesto.");
        await using var cmd = Command(db, $"SELECT to_jsonb(t)::text FROM {Quote(table)} t WHERE \"Id\" = @id");
        cmd.Parameters.AddWithValue("id", id);
        return await cmd.ExecuteScalarAsync() as string;
    }

    private static NpgsqlCommand Command(AppDbContext db, string sql) => new(sql,
        (NpgsqlConnection)db.Database.GetDbConnection(),
        (NpgsqlTransaction)db.Database.CurrentTransaction!.GetDbTransaction());

    public static Manifest Read(string path, string database)
    {
        var m = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(path))
            ?? throw new InvalidOperationException("Manifiesto vacío.");
        if (m.Version != 1 || m.Database != database || m.Rows.Count == 0
            || m.Rows.Select(r => (r.Table, r.Id)).Distinct().Count() != m.Rows.Count)
            throw new InvalidOperationException("Manifiesto inválido o correspondiente a otra base. No se modificó nada.");
        return m;
    }

    public void Write(string path)
    {
        // Flush before database commit: a crash can leave a pending manifest, never untracked rows.
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, this, new JsonSerializerOptions { WriteIndented = true });
        stream.Flush(flushToDisk: true);
    }

    public async Task<bool> Verify(AppDbContext db)
    {
        var found = 0;
        foreach (var row in Rows)
        {
            var actual = await Snapshot(db, row.Table, row.Id);
            if (actual is null) continue;
            found++;
            if (actual != row.Snapshot)
                throw new InvalidOperationException($"Fila sembrada modificada: {row.Table}/{row.Id}. Se aborta para preservar cambios posteriores.");
        }
        if (found == 0)
        {
            // Only a rolled-back apply / fully committed undo is safe to clear.
            foreach (var change in Changes)
            {
                var customer = await db.Customers.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(c => c.Id == change.Id);
                if (customer is null || customer.LastVisitAt != change.Before)
                    throw new InvalidOperationException("No quedan filas sembradas pero LastVisitAt no está restaurado. Se requiere revisión manual; se conserva el manifiesto.");
            }
            return false;
        }
        if (found != Rows.Count)
            throw new InvalidOperationException("Faltan filas del manifiesto: se requiere revisión manual; no se borró nada.");
        foreach (var change in Changes)
        {
            var customer = await db.Customers.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(c => c.Id == change.Id);
            if (customer is null || customer.LastVisitAt != change.After)
                throw new InvalidOperationException($"LastVisitAt cambió posteriormente en {change.Id}; undo se aborta sin sobrescribirlo.");
        }
        return true;
    }

    public async Task CheckReferences(AppDbContext db)
    {
        var seeded = Rows.GroupBy(r => r.Table).ToDictionary(g => g.Key, g => g.Select(r => r.Id).ToArray());
        var conflicts = new List<string>();
        // Check every incoming FK in the EF model, including cascades and soft-deleted rows.
        // A real row attached later to a seeded session/delivery must NEVER be cascade-deleted.
        foreach (var entity in db.Model.GetEntityTypes())
        foreach (var fk in entity.GetForeignKeys())
        {
            if (!seeded.TryGetValue(fk.PrincipalEntityType.GetTableName()!, out var parentIds)) continue;
            if (fk.Properties.Count != 1 || fk.PrincipalKey.Properties.Single().Name != "Id")
                throw new InvalidOperationException("FK no soportada; se requiere revisión antes de undo.");
            var table = entity.GetTableName()!;
            var column = fk.Properties[0].GetColumnName(StoreObjectIdentifier.Table(table, entity.GetSchema()))!;
            await using var cmd = Command(db, $"SELECT count(*) FROM {Quote(table)} WHERE {Quote(column)} = ANY(@parents) AND NOT (\"Id\" = ANY(@owned))");
            cmd.Parameters.AddWithValue("parents", parentIds);
            cmd.Parameters.AddWithValue("owned", seeded.GetValueOrDefault(table) ?? []);
            var count = (long)(await cmd.ExecuteScalarAsync())!;
            if (count != 0)
                conflicts.Add($"{count} filas ajenas en {table}.{column} referencian la simulación.");
        }
        if (conflicts.Count > 0)
            throw new InvalidOperationException("Undo abortado para preservar actividad ajena:\n- " + string.Join("\n- ", conflicts));
    }

    public async Task Undo(AppDbContext db, bool includeCustomers = false)
    {
        await CheckReferences(db);
        var seeded = Rows.GroupBy(r => r.Table).ToDictionary(g => g.Key, g => g.Select(r => r.Id).ToArray());
        // Delete children first; ExecuteSqlRaw intentionally bypasses soft deletion.
        string[] order = ["ContainerMovements", "SessionStockMovements", "DeliveryItems", "Payments",
            "SessionCashSettlements", "VehicleLoads", "Deliveries", "DeliverySessions"];
        if (includeCustomers) order = [.. order, "CustomerContainerBalances", "Customers"];
        if (seeded.Keys.Except(order).Any()) throw new InvalidOperationException("Manifiesto contiene tablas no sembrables.");
        foreach (var table in order)
        {
            if (!seeded.TryGetValue(table, out var ids)) continue;
            await using var cmd = Command(db, $"DELETE FROM {Quote(table)} WHERE \"Id\" = ANY(@ids)");
            cmd.Parameters.AddWithValue("ids", ids);
            await cmd.ExecuteNonQueryAsync();
        }
        foreach (var change in Changes)
            await db.Customers.IgnoreQueryFilters().Where(c => c.Id == change.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.LastVisitAt, change.Before));
    }
}
