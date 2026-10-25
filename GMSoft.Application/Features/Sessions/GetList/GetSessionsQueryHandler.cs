using GMSoft.Application.Common;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.Sessions.Common;
using MediatR;

namespace GMSoft.Application.Features.Sessions.GetList;

public class GetSessionsQueryHandler : IRequestHandler<GetSessionsQuery, PagedResult<SessionDto>>
{
    private readonly ISessionRepository _sessions;

    public GetSessionsQueryHandler(ISessionRepository sessions)
    {
        _sessions = sessions;
    }

    public async Task<PagedResult<SessionDto>> Handle(
        GetSessionsQuery request,
        CancellationToken cancellationToken)
    {
        // La traduccion de dia local a rango UTC vive aca y no en el repositorio: es
        // una regla del negocio (cual es "el dia del reparto"), no una de la base.
        DateTime? desdeUtc = null;
        DateTime? hastaUtc = null;

        if (request.Date is not null)
            (desdeUtc, hastaUtc) = BusinessTime.DayRangeUtc(request.Date.Value);

        DateTime? cerradaDesdeUtc = null;
        DateTime? cerradaHastaUtc = null;

        if (request.ClosedDate is not null)
            (cerradaDesdeUtc, cerradaHastaUtc) = BusinessTime.DayRangeUtc(request.ClosedDate.Value);

        var (items, totalCount) = await _sessions.GetPagedAsync(
            request.Page,
            request.PageSize,
            request.DriverId,
            request.ZoneId,
            request.VehicleId,
            desdeUtc,
            hastaUtc,
            cancellationToken,
            cerradaDesdeUtc,
            cerradaHastaUtc,
            request.IncludeOpen);

        // Las recibidas traen lo que quedó colgado (faltantes y sobrantes), en una sola
        // consulta para toda la página: Recepción muestra si cada una cuadró. Las que
        // siguen en la calle no: su stock cambia con cada venta y se mira en el detalle.
        var stock = await _sessions.GetStockBalancesAsync(
            items.Where(s => s.Status == GMSoft.Domain.Enums.SessionStatus.Closed).Select(s => s.Id).ToList(),
            cancellationToken);
        var dtos = items
            .Select(s => SessionMapping.ToDto(s, stock.GetValueOrDefault(s.Id) ?? []))
            .ToList();

        return new PagedResult<SessionDto>(dtos, totalCount, request.Page, request.PageSize);
    }
}
