using GMSoft.Domain.Common;

namespace GMSoft.Domain.Entities;

/// <summary>
/// Un producto dentro de una entrega, con el precio congelado y el intercambio
/// de envases que hubo por esa línea.
/// </summary>
public class DeliveryItem : BaseEntity
{
    public Guid DeliveryId { get; set; }
    public Delivery Delivery { get; set; } = null!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int Quantity { get; set; }

    /// <summary>
    /// Precio con el que se vendió, copiado del producto al momento de la entrega.
    /// No se lee de Product: si apuntara al precio actual, un aumento reescribiría
    /// la historia y las entregas viejas mostrarían plata que nunca se cobró.
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Envases que quedaron en poder del cliente por esta línea.</summary>
    public int ContainersOut { get; set; }

    /// <summary>Envases vacíos que el cliente devolvió por esta línea.</summary>
    public int ContainersIn { get; set; }
}
