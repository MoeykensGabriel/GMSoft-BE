namespace GMSoft.Application.Features.Vehicles.LoadStatus;

/// <summary>
/// Cómo está cada camión de cara a la carga del depósito. Lo que decide si se lo
/// puede cargar son estas dos cosas y nada más: si está en la calle, y si ya tiene
/// mercadería arriba esperando salir.
/// </summary>
public record VehicleLoadStatusDto(
    Guid   Id,
    string Name,
    string LicensePlate,

    /// <summary>Tiene una salida abierta. No está en el depósito, no se puede cargar.</summary>
    bool   IsOnRoute,

    /// <summary>Unidades ya cargadas y sin salir. Cero es un camión vacío.</summary>
    int    PendingUnits);
