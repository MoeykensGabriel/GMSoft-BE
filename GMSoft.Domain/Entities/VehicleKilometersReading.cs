using GMSoft.Domain.Common;
using GMSoft.Domain.Enums;

namespace GMSoft.Domain.Entities;

/// <summary>
/// El km que el chofer leyó en el tablero al salir, cuando el del sistema no era
/// confiable. La oficina lo acepta (pasa a ser el km real) o lo rechaza (se pide de nuevo).
/// </summary>
public class VehicleKilometersReading : BaseEntity
{
    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    public Guid DeliverySessionId { get; set; }
    public DeliverySession DeliverySession { get; set; } = null!;
    public int Kilometers { get; set; }
    /// <summary>Lo que tenía el sistema antes de la lectura.</summary>
    public int PreviousKilometers { get; set; }
    public DateTime ReadAt { get; set; }
    public Guid? ReadByUserId { get; set; }
    public VehicleKilometersReadingStatus Status { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public Guid? ResolvedByUserId { get; set; }
}
