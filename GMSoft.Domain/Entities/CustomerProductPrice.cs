using GMSoft.Domain.Common;

namespace GMSoft.Domain.Entities;

/// <summary>
/// Precio particular de un producto para un cliente. Si no hay fila, vale el
/// precio del producto. Una fila por cliente y producto.
/// </summary>
public class CustomerProductPrice : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public decimal Price { get; set; }
}
