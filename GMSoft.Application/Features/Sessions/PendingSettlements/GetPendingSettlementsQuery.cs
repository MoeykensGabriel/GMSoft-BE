using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Sessions.Common;
using MediatR;

namespace GMSoft.Application.Features.Sessions.PendingSettlements;

/// <summary>
/// Una salida recibida a la que todavía no se le cargó la plata. <see cref="CashToDeliver"/>
/// es el efectivo que cobró el chofer: contra eso se compara lo que entregue al rendir.
/// </summary>
public record PendingSettlementDto(
    Guid SessionId, string DriverName, string VehicleName, string VehicleLicensePlate,
    string ZoneName, DateTime ClosedAt, decimal CashToDeliver);

/// <summary>
/// Las salidas recibidas sin rendir, de cualquier día. Recibir con «la plata llega
/// después» las deja así y, sin esta lista, solo se ven entrando a cada salida.
/// </summary>
public record GetPendingSettlementsQuery : IRequest<IReadOnlyList<PendingSettlementDto>>;

public class GetPendingSettlementsQueryHandler(ISessionRepository sessions)
    : IRequestHandler<GetPendingSettlementsQuery, IReadOnlyList<PendingSettlementDto>>
{
    public async Task<IReadOnlyList<PendingSettlementDto>> Handle(
        GetPendingSettlementsQuery request, CancellationToken cancellationToken)
    {
        var pending = await sessions.GetClosedUnsettledAsync(cancellationToken);
        // El Home pregunta cada minuto: sin pendientes no hace falta sumar cobros.
        if (pending.Count == 0) return [];

        var cash = await sessions.GetCashCollectedAsync(pending.Select(s => s.Id).ToList(), cancellationToken);
        // La más vieja arriba: es la plata que hace más tiempo que falta.
        return pending
            .OrderBy(s => s.ClosedAt).ThenBy(s => s.Id)
            .Select(s => new PendingSettlementDto(
                s.Id,
                s.Driver is null ? string.Empty : $"{s.Driver.FirstName} {s.Driver.LastName}".Trim(),
                s.Vehicle?.Name ?? string.Empty,
                s.Vehicle?.LicensePlate ?? string.Empty,
                SessionMapping.ZoneLabel(s),
                s.ClosedAt!.Value,
                cash.GetValueOrDefault(s.Id)))
            .ToList();
    }
}
