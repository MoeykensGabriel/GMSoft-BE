using GMSoft.Domain.Common;
using GMSoft.Domain.Enums;

namespace GMSoft.Domain.Entities;

/// <summary>
/// La visita a un cliente dentro de una sesion de reparto. Puede ser una venta,
/// o un paso solo a retirar envases.
/// </summary>
public class Delivery : BaseEntity
{
    public Guid DeliverySessionId { get; set; }
    public DeliverySession DeliverySession { get; set; } = null!;

    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public DeliveryType Type { get; set; }

    public DateTime DeliveredAt { get; set; }

    /// <summary>
    /// Total de la visita, suma de los items con el precio del momento. Es lo que
    /// suma a la deuda del cliente. En un retiro de envases es cero.
    /// </summary>
    public decimal Total { get; set; }

    public string? Notes { get; set; }

    /// <summary>Lo que se le vendio. Vacio en un retiro de envases.</summary>
    public ICollection<DeliveryItem> Items { get; set; } = new List<DeliveryItem>();

    /// <summary>
    /// Los envases que se movieron en la visita, en las dos direcciones: los que
    /// quedaron en poder del cliente y los vacios que devolvio. Van al libro mayor
    /// de envases y no a la linea de venta, porque mover un envase no depende de
    /// vender: el cliente puede devolver tres bidones y no llevarse nada, o
    /// llevarse uno prestado sin comprar.
    /// </summary>
    public ICollection<ContainerMovement> ContainerMovements { get; set; }
        = new List<ContainerMovement>();
}
