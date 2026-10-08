using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;

namespace GMSoft.Application.Features.Deliveries.Register;

public static class DeliveryStockRules
{
    public static async Task EnsureAvailableAsync(ISessionRepository sessions, Guid sessionId,
        IReadOnlyList<DeliveryItemLine> items, IReadOnlyDictionary<Guid, Product> products, CancellationToken ct)
    {
        var stock = await sessions.GetStockBalanceAsync(sessionId, ct);
        foreach (var item in items)
        {
            var available = stock.FirstOrDefault(s => s.ProductId == item.ProductId)?.FullOnBoard ?? 0;
            if (item.Quantity > available)
                throw new ConflictException(
                    $"No alcanza el stock de '{products[item.ProductId].Detail}': quedan {available} llenos.");
        }
    }
}

