using GMSoft.Domain.Common;
using GMSoft.Domain.Enums;

namespace GMSoft.Domain.Entities;

/// <summary>
/// Una salida de reparto: el chofer agarra el vehículo, carga el camión y sale.
/// Al volver se cierra y se concilia contra lo que entregó.
/// </summary>
public class DeliverySession : BaseEntity
{
    public Guid DriverId { get; set; }
    public Driver Driver { get; set; } = null!;

    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;

    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    public int KilometersAtOpen { get; set; }
    public int? KilometersAtClose { get; set; }

    public SessionStatus Status { get; set; } = SessionStatus.Open;

    /// <summary>Lo que se cargó al salir y lo que volvió.</summary>
    public ICollection<SessionLoadItem> LoadItems { get; set; } = new List<SessionLoadItem>();

    /// <summary>Las visitas hechas durante la salida.</summary>
    public ICollection<Delivery> Deliveries { get; set; } = new List<Delivery>();
}
