using GMSoft.Domain.Common;
using GMSoft.Domain.Enums;

namespace GMSoft.Domain.Entities;

/// <summary>
/// El libro mayor de los envases: toda salida y todo retorno queda asentado acá.
/// Es la fuente de verdad. Los saldos por cliente son una foto de estos movimientos,
/// y sirve para los dos modos de seguimiento — por saldo y por unidad.
/// </summary>
public class ContainerMovement : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    /// <summary>Cliente involucrado. Nulo en movimientos que solo afectan al depósito.</summary>
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>Entrega que lo originó. Nulo en ajustes y bajas cargados desde la oficina.</summary>
    public Guid? DeliveryId { get; set; }
    public Delivery? Delivery { get; set; }

    /// <summary>La unidad puntual, solo cuando el producto se sigue por unidad.</summary>
    public Guid? ContainerUnitId { get; set; }
    public ContainerUnit? ContainerUnit { get; set; }

    /// <summary>
    /// Positivo cuando el envase sale hacia el cliente, negativo cuando vuelve.
    /// El saldo del cliente es la suma de sus movimientos.
    /// </summary>
    public int Quantity { get; set; }

    public ContainerMovementType Type { get; set; }

    public DateTime OccurredAt { get; set; }

    /// <summary>
    /// Motivo del movimiento. Obligatorio en la práctica para ajustes y pérdidas:
    /// un saldo corregido sin explicación deja al libro mayor sin poder justificar
    /// el número.
    /// </summary>
    public string? Notes { get; set; }
}
