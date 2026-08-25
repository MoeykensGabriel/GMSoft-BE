using GMSoft.Domain.Common;
using GMSoft.Domain.Enums;

namespace GMSoft.Domain.Entities;

/// <summary>
/// Una unidad de envase identificada por su número, para los productos con
/// seguimiento ByUnit — típicamente el dispenser, que es caro y va en comodato.
/// Los bidones y sifones no llegan acá: se siguen por saldo.
/// </summary>
public class ContainerUnit : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    /// <summary>Número grabado o etiquetado. No se repite.</summary>
    public string SerialNumber { get; set; } = string.Empty;

    public ContainerUnitStatus Status { get; set; } = ContainerUnitStatus.InDepot;

    /// <summary>Quién la tiene ahora. Nulo si está en el depósito o fuera de servicio.</summary>
    public Guid? CurrentCustomerId { get; set; }
    public Customer? CurrentCustomer { get; set; }
}
