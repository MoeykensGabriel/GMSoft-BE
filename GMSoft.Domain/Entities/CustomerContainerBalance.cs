using GMSoft.Domain.Common;

namespace GMSoft.Domain.Entities;

/// <summary>
/// Cuántos envases de un producto tiene un cliente en su poder. Es una foto
/// derivada de ContainerMovement, que se actualiza en la misma transacción que
/// el movimiento para poder consultar saldos sin recorrer todo el historial.
/// Una fila por cliente y producto.
/// </summary>
public class CustomerContainerBalance : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    /// <summary>Envases en poder del cliente. No debería ser negativo.</summary>
    public int Quantity { get; set; }
}
