using GMSoft.Domain.Common;

namespace GMSoft.Domain.Entities;

/// <summary>Una tanda de llenos; sus renglones son los movimientos del libro mayor.</summary>
public class SessionRestock : BaseEntity
{
    public Guid DeliverySessionId { get; set; }
    public DeliverySession DeliverySession { get; set; } = null!;
    public Guid ClientRequestId { get; set; }
    public DateTime OccurredAt { get; set; }
    public Guid RegisteredByUserId { get; set; }
    public string? Notes { get; set; }
    public ICollection<SessionStockMovement> Items { get; set; } = new List<SessionStockMovement>();
}
