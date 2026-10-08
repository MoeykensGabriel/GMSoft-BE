using GMSoft.Domain.Entities;

namespace GMSoft.Application.Features.Sessions.Common;

public record SessionRestockDto(
    Guid Id,
    Guid SessionId,
    Guid ClientRequestId,
    DateTime OccurredAt,
    Guid RegisteredByUserId,
    string? Notes,
    IReadOnlyList<SessionRestockItemDto> Items)
{
    public static SessionRestockDto From(SessionRestock restock) => new(
        restock.Id, restock.DeliverySessionId, restock.ClientRequestId,
        restock.OccurredAt, restock.RegisteredByUserId, restock.Notes,
        restock.Items.OrderBy(i => i.ProductId).Select(i => new SessionRestockItemDto(
            i.ProductId, i.RestockProductDetail!, i.Quantity)).ToList());
}

public record SessionRestockItemDto(Guid ProductId, string ProductDetail, int Quantity);
