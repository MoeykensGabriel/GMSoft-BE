using GMSoft.Domain.Common;
using GMSoft.Domain.Enums;

namespace GMSoft.Domain.Entities;

public class Promotion : BaseEntity
{
    public string? BusinessName { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public int[] VisitDays { get; set; } = [];
    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    public Guid DriverId { get; set; }
    public Driver Driver { get; set; } = null!;
    public Guid DeliverySessionId { get; set; }
    public DeliverySession DeliverySession { get; set; } = null!;
    public Guid ZoneId { get; set; }
    public Zone Zone { get; set; } = null!;
    public DateTime RegisteredAt { get; set; }
    public DateOnly PickupDate { get; set; }
    public PromotionStatus Status { get; set; }
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid ClientRequestId { get; set; }
    public Guid? CloseClientRequestId { get; set; }
    public DateTime? ClosedAt { get; set; }
    public Guid? ClosedByDriverId { get; set; }
    public Guid? ClosingSessionId { get; set; }
    public ICollection<PromotionLine> Lines { get; set; } = new List<PromotionLine>();
    public ICollection<PromotionContainerMovement> ContainerMovements { get; set; } = new List<PromotionContainerMovement>();
}

public class PromotionLine : BaseEntity
{
    public Guid PromotionId { get; set; }
    public Promotion Promotion { get; set; } = null!;
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; }
    public int ContainersLoaned { get; set; }
    public int ContainersReturned { get; set; }
    public int ContainersLost { get; set; }
}

// El enlace vive en una tabla nueva: no cambia el libro mayor existente.
public class PromotionContainerMovement : BaseEntity
{
    public Guid PromotionId { get; set; }
    public Promotion Promotion { get; set; } = null!;
    public Guid ContainerMovementId { get; set; }
    public ContainerMovement ContainerMovement { get; set; } = null!;
}

public class PromotionSettings
{
    public int Id { get; set; } = 1;
    public int PickupDays { get; set; } = 7;
}

