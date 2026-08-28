using MediatR;

namespace GMSoft.Application.Features.Sessions.Open;

/// <summary>
/// El chofer entra, carga el kilometraje del vehiculo que tiene asignado, elige la
/// zona y declara lo que sube al camion. El vehiculo no viene en el comando: sale
/// de su asignacion, para que no pueda salir con uno que no es el suyo.
/// </summary>
public record OpenSessionCommand(
    Guid ZoneId,
    int  KilometersAtOpen,
    IReadOnlyList<OpenSessionLoadLine> Load) : IRequest<Guid>;

public record OpenSessionLoadLine(Guid ProductId, int Quantity);
