using System.Text.Json;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Data.Context;
using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.Data.Repositories;

public class WorkingHoursRepository(AppDbContext context) : IWorkingHoursRepository
{
    public Task<WorkingHoursSettings?> GetAsync(CancellationToken ct) =>
        context.Set<WorkingHoursSettings>().AsNoTracking().SingleOrDefaultAsync(ct);

    // Reemplazo atómico de la semana, incluso si dos administradores crean la primera fila.
    public async Task SetAsync(WorkingDay[] days, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(days);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""INSERT INTO "WorkingHoursSettings" ("Id", "Days") VALUES (1, CAST({json} AS jsonb)) ON CONFLICT ("Id") DO UPDATE SET "Days" = EXCLUDED."Days" """, ct);
    }
}
