using GMSoft.Domain.Common;
using GMSoft.Domain.Enums;

namespace GMSoft.Domain.Entities;

/// <summary>
/// El libro mayor de los envases, y la unica tabla donde se mueven. Toda salida y
/// todo retorno queda asentado aca: los de una visita, los ajustes de la oficina y
/// las bajas por perdida. Sirve para los dos modos de seguimiento, por saldo y por
/// unidad.
///
/// Que sea el unico lugar es deliberado: si los envases se contaran tambien en la
/// linea de venta, habria dos numeros para el mismo envase y tarde o temprano no
/// coincidirian.
/// </summary>
public class ContainerMovement : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    /// <summary>Cliente involucrado. Nulo en movimientos que solo afectan al deposito.</summary>
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>
    /// La visita que lo origino. Nulo en ajustes y bajas cargados desde la oficina.
    /// Los envases que salen y los que vuelven en una misma visita son dos filas
    /// con este mismo DeliveryId.
    /// </summary>
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
    /// Quien lo registro. Guid de la cuenta, sin navegacion. Sin esto el control
    /// queda a medias: se sabe que un envase se perdio pero no quien lo anoto.
    /// </summary>
    public Guid? RegisteredByUserId { get; set; }

    /// <summary>
    /// Motivo del movimiento. Obligatorio en la practica para ajustes y perdidas:
    /// un saldo corregido sin explicacion deja al libro mayor sin poder justificar
    /// el numero.
    /// </summary>
    public string? Notes { get; set; }
}
