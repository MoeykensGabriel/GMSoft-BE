using GMSoft.Domain.Common;

namespace GMSoft.Domain.Entities;

/// <summary>
/// Envases vacios que el cliente devolvio en una visita. Separado de la venta
/// porque devolver no depende de comprar, y porque puede devolver envases de un
/// producto distinto al que compra ese dia.
/// </summary>
public class DeliveryContainerReturn : BaseEntity
{
    public Guid DeliveryId { get; set; }
    public Delivery Delivery { get; set; } = null!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int Quantity { get; set; }
}
