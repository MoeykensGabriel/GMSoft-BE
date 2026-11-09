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

    /// <summary>
    /// La próxima salida lee el km del tablero: esa sola vez el chofer puede poner menos que
    /// <see cref="CurrentKilometers"/>. Se apaga al abrir (queda la lectura para la oficina)
    /// y vuelve solo si la oficina la rechaza.
    /// </summary>
    public bool KilometersCheckPending { get; set; }

    /// <summary>
    /// Cuándo salió el camión con el km que la oficina aceptó. Desde ahí cuentan los
    /// promedios de kilometraje; lo de antes no era confiable.
    /// </summary>
    public DateTime? KilometersCheckedAt { get; set; }

    /// <summary>Choferes que lo tienen asignado. Puede ser más de uno.</summary>
    public ICollection<Driver> Drivers { get; set; } = new List<Driver>();

    /// <summary>
    /// Sesiones de reparto hechas con este vehículo. Que tenga una sesión abierta
    /// se responde desde acá (Status == Open) y no con un booleano propio: dos
    /// fuentes para el mismo estado terminan desincronizadas.
    /// </summary>
    public ICollection<DeliverySession> Sessions { get; set; } = new List<DeliverySession>();

    /// <summary>
    /// Cargas que la oficina le subió. Las que todavía no salieron son las que se
    /// lleva la próxima salida.
    /// </summary>
    public ICollection<VehicleLoad> PendingLoads { get; set; } = new List<VehicleLoad>();
}
