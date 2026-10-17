using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Data.Context;
using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.Data.Repositories;

public class ContainerBalanceRepository
    : Repository<CustomerContainerBalance>, IContainerBalanceRepository
{
    public ContainerBalanceRepository(AppDbContext context) : base(context) { }

    public async Task<CustomerContainerBalance?> GetAsync(
        Guid customerId,
        Guid productId,
        CancellationToken cancellationToken = default)
        => await _context.CustomerContainerBalances
            .FirstOrDefaultAsync(
                b => b.CustomerId == customerId && b.ProductId == productId,
                cancellationToken);

    public async Task AdjustAsync(
        Guid customerId,
        Guid productId,
        int delta,
        CancellationToken cancellationToken = default)
    {
        // Una sola sentencia que crea la fila o le suma: leer, sumar en memoria y guardar
        // perdía movimientos cuando el chofer y la oficina tocaban el mismo saldo a la vez.
        // Se ejecuta en el momento, dentro de la transacción del llamador.
        var ahora = DateTime.UtcNow;
        await _context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "CustomerContainerBalances" ("Id", "CustomerId", "ProductId", "Quantity", "CreatedAt", "UpdatedAt", "IsDeleted")
            VALUES ({Guid.NewGuid()}, {customerId}, {productId}, {delta}, {ahora}, {ahora}, FALSE)
            ON CONFLICT ("CustomerId", "ProductId") DO UPDATE
            SET "Quantity" = "CustomerContainerBalances"."Quantity" + EXCLUDED."Quantity", "UpdatedAt" = EXCLUDED."UpdatedAt"
            """, cancellationToken);
        await RefreshTrackedAsync(customerId, productId, cancellationToken);
    }

    public async Task<int> LockQuantityAsync(
        Guid customerId,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        // Asegura la fila para poder bloquearla aunque el cliente nunca haya tenido envases.
        var ahora = DateTime.UtcNow;
        await _context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "CustomerContainerBalances" ("Id", "CustomerId", "ProductId", "Quantity", "CreatedAt", "UpdatedAt", "IsDeleted")
            VALUES ({Guid.NewGuid()}, {customerId}, {productId}, 0, {ahora}, {ahora}, FALSE)
            ON CONFLICT ("CustomerId", "ProductId") DO NOTHING
            """, cancellationToken);
        return await _context.Database.SqlQuery<int>($"""
            SELECT "Quantity" AS "Value" FROM "CustomerContainerBalances"
            WHERE "CustomerId" = {customerId} AND "ProductId" = {productId}
            FOR UPDATE
            """).SingleAsync(cancellationToken);
    }

    /// <summary>
    /// Si la transacción ya había leído este saldo, la copia en memoria queda vieja después
    /// del UPDATE en SQL: se recarga para que la siguiente lectura vea el valor real.
    /// </summary>
    private async Task RefreshTrackedAsync(Guid customerId, Guid productId, CancellationToken cancellationToken)
    {
        var tracked = _context.ChangeTracker.Entries<CustomerContainerBalance>()
            .FirstOrDefault(e => e.Entity.CustomerId == customerId && e.Entity.ProductId == productId);
        if (tracked is not null) await tracked.ReloadAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerContainerBalance>> GetByCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
        => await _context.CustomerContainerBalances
            .AsNoTracking()
            .Include(b => b.Product)
            .Where(b => b.CustomerId == customerId)
            .OrderBy(b => b.Product.Detail)
            .ToListAsync(cancellationToken);
}
