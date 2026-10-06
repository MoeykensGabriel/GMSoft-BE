using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using MediatR;

namespace GMSoft.Application.Features.Sessions.ActiveDepartures;

public record DepartureLoadLineDto(Guid ProductId, string ProductDetail, long Quantity);

public record ActiveDepartureDto(
    Guid SessionId, Guid VehicleId, string VehicleName, string VehicleLicensePlate,
    string DriverName, string ZoneName, DateTime OpenedAt,
    IReadOnlyList<DepartureLoadLineDto> InitialLoad);

public record GetActiveDeparturesQuery : IRequest<IReadOnlyList<ActiveDepartureDto>>;

public class GetActiveDeparturesQueryHandler(ISessionRepository sessions)
    : IRequestHandler<GetActiveDeparturesQuery, IReadOnlyList<ActiveDepartureDto>>
{
    public async Task<IReadOnlyList<ActiveDepartureDto>> Handle(
        GetActiveDeparturesQuery request, CancellationToken cancellationToken)
        => (await sessions.GetOpenWithInitialLoadAsync(cancellationToken))
            .Where(s => s.Status == SessionStatus.Open)
            .OrderBy(s => s.Vehicle?.Name).ThenBy(s => s.Id)
            .Select(ActiveDepartureMapping.ToDto).ToList();
}

public static class ActiveDepartureMapping
{
    public static ActiveDepartureDto ToDto(DeliverySession session) => new(
        session.Id, session.VehicleId, session.Vehicle?.Name ?? string.Empty,
        session.Vehicle?.LicensePlate ?? string.Empty,
        session.Driver is null ? string.Empty : $"{session.Driver.FirstName} {session.Driver.LastName}".Trim(),
        session.Zone?.Name ?? string.Empty, session.OpenedAt,
        session.StockMovements
            .Where(m => m.Type == SessionStockMovementType.InitialLoad && m.State == ContainerState.Full)
            .GroupBy(m => m.ProductId)
            .Select(g => new DepartureLoadLineDto(g.Key, g.First().Product?.Detail ?? string.Empty,
                g.Sum(m => (long)m.Quantity)))
            .OrderBy(line => line.ProductDetail).ThenBy(line => line.ProductId).ToList());
}
