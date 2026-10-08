using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;

namespace GMSoft.Application.Features.Promotions.Common;

public static class PromotionMovements
{
    public static void Add(Promotion p, Guid productId, int quantity, ContainerMovementType type,
        DateTime now, Guid? userId, string reason, Guid? customerId = null)
    {
        p.ContainerMovements.Add(new PromotionContainerMovement
        {
            ContainerMovement = new ContainerMovement
            {
                ProductId = productId, Quantity = quantity, Type = type, CustomerId = customerId,
                OccurredAt = now, RegisteredByUserId = userId,
                Notes = $"Promocion {p.ClientRequestId}: {reason}"
            }
        });
    }
}

