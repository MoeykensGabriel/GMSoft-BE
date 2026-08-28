using GMSoft.Domain.Common;
using GMSoft.Domain.Enums;

namespace GMSoft.Domain.Entities;

/// <summary>
/// Una salida de reparto: el chofer agarra el vehiculo, carga el camion y sale.
/// Al volver se cierra, se descarga lo que sobro y se rinde la plata.
/// </summary>
public class DeliverySession : BaseEntity
{
    public Guid DriverId { get; set; }
    public Driver Driver { get; set; } = null!;

    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;

    /// <summary>
    /// Zona que sale a repartir. La elige el chofer al abrir la sesion, junto con
    /// el kilometraje. El recorrido del dia son los clientes de esta zona.
    /// </summary>
    public Guid ZoneId { get; set; }
    public Zone Zone { get; set; } = null!;

    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    public int KilometersAtOpen { get; set; }
    public int? KilometersAtClose { get; set; }

    public SessionStatus Status { get; set; } = SessionStatus.Open;

    /// <summary>
    /// Libro mayor del stock a bordo: carga, recargas en ruta, entregas, vacios
    /// levantados y descarga final. El faltante es el saldo que queda al cerrar.
    /// </summary>
    public ICollection<SessionStockMovement> StockMovements { get; set; }
        = new List<SessionStockMovement>();

    /// <summary>Las visitas hechas durante la salida.</summary>
    public ICollection<Delivery> Deliveries { get; set; } = new List<Delivery>();

    /// <summary>La rendicion de plata, una vez que el admin la recibio y conto.</summary>
    public SessionCashSettlement? CashSettlement { get; set; }
}
