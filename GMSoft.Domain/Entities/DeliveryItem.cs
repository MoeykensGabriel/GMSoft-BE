using GMSoft.Domain.Common;

namespace GMSoft.Domain.Entities;

/// <summary>
/// Un producto vendido dentro de una visita, con el precio congelado.
/// </summary>
public class DeliveryItem : BaseEntity
{
    public Guid DeliveryId { get; set; }
    public Delivery Delivery { get; set; } = null!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int Quantity { get; set; }

    /// <summary>
    /// Precio con el que se vendio. Sale del precio particular del cliente si tiene
    /// uno, y si no del precio del producto, pero se copia al momento de la venta.
    /// No se lee por FK: si apuntara al precio actual, un aumento reescribiria la
    /// historia y las entregas viejas mostrarian plata que nunca se cobro.
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Envases que quedaron en poder del cliente por esta linea. Normalmente
    /// coincide con la cantidad, pero no siempre: se puede recargar un envase que
    /// el cliente ya tenia.
    /// </summary>
    public int ContainersOut { get; set; }
}
