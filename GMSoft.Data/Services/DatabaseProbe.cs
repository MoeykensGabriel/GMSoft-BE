using GMSoft.Application.Common.Interfaces;
using GMSoft.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GMSoft.Data.Services;

public class DatabaseProbe(AppDbContext db, ILogger<DatabaseProbe> logger) : IDatabaseProbe
{
    // Menos que lo que espera un monitor: una base colgada tiene que contar como caída.
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(5);

    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(Limit);
        try
        {
            return await db.Database.CanConnectAsync(limit.Token);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "La base no respondió al chequeo de salud.");
            return false;
        }
    }
}
