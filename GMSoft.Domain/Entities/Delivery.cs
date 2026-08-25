using GMSoft.Domain.Common;

namespace GMSoft.Domain.Entities;

/// <summary>
/// La visita a un cliente dentro de una sesión de reparto: lo que se le dejó,
/// lo que se le retiró y cuánto sumó a su cuenta.
/// </summary>
public class Delivery : BaseEntity
{
    public Guid DeliverySessionId { get; set; }
    public DeliverySession DeliverySession { get; set; } = null!;

    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public DateTime DeliveredAt { get; set; }

    /// <summary>
    /// Total de la entrega, suma de los ítems con el precio del momento. Es lo que
    /// suma a la deuda del cliente.
    /// </summary>
    public decimal Total { get; set; }

    public string? Notes { get; set; }

    public ICollection<DeliveryItem> Items { get; set; } = new List<DeliveryItem>();
}
