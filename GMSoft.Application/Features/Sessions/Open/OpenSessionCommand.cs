using MediatR;

namespace GMSoft.Application.Features.Sessions.Open;

/// <summary>
/// El chofer entra, carga el kilometraje del vehículo que tiene asignado y elige la
/// zona. El vehículo no viene en el comando: sale de su asignación, para que no
/// pueda salir con uno que no es el suyo.
///
/// La carga tampoco viene: la subió la oficina antes de que él llegara, y la salida
/// se lleva lo que el camión tenga arriba sin salir. Que el chofer declarara la
/// carga convertía el control en una copia de lo que él mismo dijo.
/// </summary>
/// <remarks>
/// <c>ZoneIds</c> son las zonas que recorre en orden; sin ellas vale <c>ZoneId</c> sola
/// (lo que mandan las versiones anteriores de la app).
/// </remarks>
public record OpenSessionCommand(Guid ZoneId, int KilometersAtOpen,
    bool ConfirmOutsideWorkingHours = false, Guid? DriverId = null, Guid[]? ZoneIds = null) : IRequest<Guid>
{
    public const int MaxZones = 5;

    public Guid[] EffectiveZoneIds => ZoneIds is { Length: > 0 } ? ZoneIds : [ZoneId];
}
