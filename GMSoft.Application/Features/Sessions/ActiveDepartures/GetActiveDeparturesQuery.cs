using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Sessions.Common;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using MediatR;

namespace GMSoft.Application.Features.Sessions.ActiveDepartures;

public record DepartureLoadLineDto(Guid ProductId, string ProductDetail, long Quantity);

/// <summary>
/// Cómo va un producto en la salida: con cuántos llenos salió, cuántos le recargaron,
/// cuántos entregó y cuántos llenos y vacíos lleva a bordo ahora.
/// </summary>
public record DepartureStockLineDto(Guid ProductId, string ProductDetail,
    long Initial, long Restocked, long Delivered, long FullOnBoard, long EmptyOnBoard);

public record ActiveDepartureDto(
    Guid SessionId, Guid VehicleId, string VehicleName, string VehicleLicensePlate,
    string DriverName, string ZoneName, DateTime OpenedAt,
    IReadOnlyList<DepartureLoadLineDto> InitialLoad)
{
    public IReadOnlyList<DepartureStockLineDto> Stock { get; init; } = [];
}

public record GetActiveDeparturesQuery : IRequest<IReadOnlyList<ActiveDepartureDto>>;

public class GetActiveDeparturesQueryHandler(ISessionRepository sessions)
    : IRequestHandler<GetActiveDeparturesQuery, IReadOnlyList<ActiveDepartureDto>>
{
    public async Task<IReadOnlyList<ActiveDepartureDto>> Handle(
        GetActiveDeparturesQuery request, CancellationToken cancellationToken)
        // En el orden en que salieron: el primero que salió, arriba.
        => (await sessions.GetOpenWithStockAsync(cancellationToken))
            .Where(s => s.Status == SessionStatus.Open)
            .OrderBy(s => s.OpenedAt).ThenBy(s => s.Vehicle?.Name).ThenBy(s => s.Id)
            .Select(ActiveDepartureMapping.ToDto).ToList();
}

public static class ActiveDepartureMapping
{
    public static ActiveDepartureDto ToDto(DeliverySession session) => new(
        session.Id, session.VehicleId, session.Vehicle?.Name ?? string.Empty,
        session.Vehicle?.LicensePlate ?? string.Empty,
        session.Driver is null ? string.Empty : $"{session.Driver.FirstName} {session.Driver.LastName}".Trim(),
        SessionMapping.ZoneLabel(session), session.OpenedAt,
        session.StockMovements
            .Where(m => m.Type == SessionStockMovementType.InitialLoad && m.State == ContainerState.Full)
            .GroupBy(m => m.ProductId)
            .Select(g => new DepartureLoadLineDto(g.Key, g.First().Product?.Detail ?? string.Empty,
                g.Sum(m => (long)m.Quantity)))
            .OrderBy(line => line.ProductDetail).ThenBy(line => line.ProductId).ToList())
    {
        // Los movimientos van con signo: lo que hay a bordo es la suma de cada estado.
        Stock = session.StockMovements
            .GroupBy(m => m.ProductId)
            .Select(g => new DepartureStockLineDto(g.Key, g.First().Product?.Detail ?? string.Empty,
                Initial: g.Where(m => m.Type == SessionStockMovementType.InitialLoad && m.State == ContainerState.Full).Sum(m => (long)m.Quantity),
                Restocked: g.Where(m => m.Type == SessionStockMovementType.Restock && m.State == ContainerState.Full).Sum(m => (long)m.Quantity),
                Delivered: -g.Where(m => m.Type == SessionStockMovementType.Delivered && m.State == ContainerState.Full).Sum(m => (long)m.Quantity),
                FullOnBoard: g.Where(m => m.State == ContainerState.Full).Sum(m => (long)m.Quantity),
                EmptyOnBoard: g.Where(m => m.State == ContainerState.Empty).Sum(m => (long)m.Quantity)))
            .OrderBy(line => line.ProductDetail).ThenBy(line => line.ProductId).ToList(),
    };
}
