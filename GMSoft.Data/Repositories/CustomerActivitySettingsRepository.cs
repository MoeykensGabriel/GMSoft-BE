using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Data.Context;
using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.Data.Repositories;

public class CustomerActivitySettingsRepository(AppDbContext context) : ICustomerActivitySettingsRepository
{
    public CustomerActivitySettings? Get() =>
        context.Set<CustomerActivitySettings>().AsNoTracking().SingleOrDefault();

    public Task<CustomerActivitySettings?> GetAsync(CancellationToken ct) =>
        context.Set<CustomerActivitySettings>().AsNoTracking().SingleOrDefaultAsync(ct);

    // Reemplazo atómico, incluso si dos administradores crean la primera fila a la vez.
    public Task SetAsync(int red, int black, CancellationToken ct) =>
        context.Database.ExecuteSqlInterpolatedAsync(
            $"""INSERT INTO "CustomerActivitySettings" ("Id", "RedAfterMissedWeeks", "BlackAfterMissedWeeks") VALUES (1, {red}, {black}) ON CONFLICT ("Id") DO UPDATE SET "RedAfterMissedWeeks" = EXCLUDED."RedAfterMissedWeeks", "BlackAfterMissedWeeks" = EXCLUDED."BlackAfterMissedWeeks" """, ct);
}
