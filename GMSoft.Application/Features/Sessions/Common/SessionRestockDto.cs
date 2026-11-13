using GMSoft.Application.Common;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;

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
    public bool IsOutsideWorkingHours { get; init; }
    public static SessionRestockDto From(SessionRestock restock) => new(
        restock.Id, restock.DeliverySessionId, restock.ClientRequestId,
        restock.OccurredAt, restock.RegisteredByUserId, restock.Notes,
        restock.Items.Where(i => i.State == ContainerState.Full)
            .OrderByProduct(i => i.RestockProductDetail!).Select(i => new SessionRestockItemDto(
            i.ProductId, i.RestockProductDetail!, i.Quantity)
            {
                RefilledEmpties = -restock.Items.Where(e => e.State == ContainerState.Empty && e.ProductId == i.ProductId)
                    .Sum(e => e.Quantity)
            }).ToList())
        { IsOutsideWorkingHours = restock.IsOutsideWorkingHours };
}

public record SessionRestockItemDto(Guid ProductId, string ProductDetail, int Quantity)
{
    /// <summary>De esos llenos, cuantos eran vacios del camion que se llenaron.</summary>
    public int RefilledEmpties { get; init; }
}
