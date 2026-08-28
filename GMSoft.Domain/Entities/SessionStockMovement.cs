using GMSoft.Domain.Common;
using GMSoft.Domain.Enums;

namespace GMSoft.Domain.Entities;

/// <summary>
/// Libro mayor del stock del camion durante una sesion. Toda entrada y salida
/// queda asentada: la carga inicial, las recargas en ruta, lo entregado, los
/// vacios que subieron y lo que se descargo al cerrar.
///
/// Es un historial y no una foto a proposito. Con una foto (cargado / devuelto)
/// una recarga en ruta no tiene donde anotarse, y el faltante del cierre pasa a
/// ser una resta que no cierra.
/// </summary>
public class SessionStockMovement : BaseEntity
{
    public Guid DeliverySessionId { get; set; }
    public DeliverySession DeliverySession { get; set; } = null!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    /// <summary>Si el movimiento es de unidades llenas o de envases vacios.</summary>
    public ContainerState State { get; set; }

    /// <summary>
    /// Positivo cuando entra al camion, negativo cuando sale. El stock a bordo en
    /// cualquier momento es la suma; una sesion cerrada con saldo distinto de cero
    /// es exactamente el faltante.
    /// </summary>
    public int Quantity { get; set; }

    public SessionStockMovementType Type { get; set; }

    public DateTime OccurredAt { get; set; }

    /// <summary>La entrega que lo origino, cuando el movimiento sale de una visita.</summary>
    public Guid? DeliveryId { get; set; }
    public Delivery? Delivery { get; set; }

    /// <summary>Motivo. Imprescindible en ajustes, recargas y traspasos.</summary>
    public string? Notes { get; set; }
}
