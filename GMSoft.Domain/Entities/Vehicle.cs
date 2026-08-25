using GMSoft.Domain.Common;
using GMSoft.Domain.Enums;

namespace GMSoft.Domain.Entities;

/// <summary>
/// Vehículo de la flota con el que se hace el reparto.
/// </summary>
public class Vehicle : BaseEntity
{
    /// <summary>Nombre con el que se lo conoce en el negocio.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Patente. Identifica al vehículo, no se repite.</summary>
    public string LicensePlate { get; set; } = string.Empty;

    public VehicleType Type { get; set; }

    /// <summary>Kilómetros actuales. Se van cargando y suben con el uso.</summary>
    public int CurrentKilometers { get; set; }

    /// <summary>Choferes que lo tienen asignado. Puede ser más de uno.</summary>
    public ICollection<Driver> Drivers { get; set; } = new List<Driver>();

    /// <summary>
    /// Sesiones de reparto hechas con este vehículo. Que tenga una sesión abierta
    /// se responde desde acá (Status == Open) y no con un booleano propio: dos
    /// fuentes para el mismo estado terminan desincronizadas.
    /// </summary>
    public ICollection<DeliverySession> Sessions { get; set; } = new List<DeliverySession>();
}
