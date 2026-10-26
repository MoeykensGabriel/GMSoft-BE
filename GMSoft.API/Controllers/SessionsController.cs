using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.Sessions.AddStock;
using GMSoft.Application.Features.Sessions.Unvisited;
using GMSoft.Application.Features.Sessions.ActiveDepartures;
using GMSoft.Application.Features.Sessions.Cancel;
using GMSoft.Application.Features.Sessions.Close;
using GMSoft.Application.Features.Sessions.Common;
using GMSoft.Application.Features.Sessions.Correction;
using GMSoft.Application.Features.Sessions.RouteOrder;
using GMSoft.Application.Features.Customers.RoutePlanning;
using GMSoft.Application.Features.Sessions.DetailedSettlement;
using GMSoft.Application.Features.Sessions.DailySummary;
using GMSoft.Application.Features.Sessions.GetById;
using GMSoft.Application.Features.Sessions.GetDeliveries;
using GMSoft.Application.Features.Sessions.GetCurrent;
using GMSoft.Application.Features.Sessions.GetStatus;
using GMSoft.Application.Features.Sessions.KeepAlive;
using GMSoft.Application.Features.Sessions.GetList;
using GMSoft.Application.Features.Sessions.Open;
using GMSoft.Application.Features.Sessions.SalesSummary;
using GMSoft.Application.Features.Sessions.Settlement;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GMSoft.API.Controllers;

/// <summary>Sesiones de reparto: la salida del chofer, desde que abre hasta que rinde.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = AppRoles.AdminOrDriver)]
public class SessionsController : ControllerBase
{
    private readonly ISender _mediator;

    [HttpGet("{id:guid}/route-order")]
    [Authorize(Roles = AppRoles.Driver)]
    public async Task<ActionResult<RoutePlanningDto>> GetRouteOrder(Guid id, [FromQuery] Guid? zoneId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetDriverRouteOrderQuery(id, zoneId), ct));

    [HttpPut("{id:guid}/route-order")]
    [Authorize(Roles = AppRoles.Driver)]
    public async Task<ActionResult<RoutePlanningDto>> SaveRouteOrder(Guid id, SaveDriverRouteOrderCommand command, CancellationToken ct)
        => Ok(await _mediator.Send(command with { Id = id }, ct));

    public SessionsController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Camiones en la calle con la carga de productos llenos con la que salieron.</summary>
    [HttpGet("active-departures")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<IReadOnlyList<ActiveDepartureDto>>> GetActiveDepartures(CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetActiveDeparturesQuery(), cancellationToken));

    /// <summary>
    /// Abre la salida: kilometraje, zona y lo que sube al camion. El vehiculo sale
    /// de la asignacion del chofer.
    /// </summary>
    [HttpPost("open")]
    [Authorize(Roles = AppRoles.AdminOrDriver)]
    public async Task<ActionResult<Guid>> Open(
        OpenSessionCommand command,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    /// <summary>
    /// El control de recepcion: cierra la salida con el kilometraje de vuelta y lo
    /// que se descarga, y devuelve el faltante si lo hay.
    ///
    /// Solo del admin. Es la oficina la que cuenta lo que volvio: si contara el
    /// mismo chofer, el control seria una copia de lo que el ya dijo.
    /// </summary>
    [HttpPost("{id:guid}/close")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<CloseSessionResult>> Close(
        Guid id,
        CloseSessionCommand command,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(command with { Id = id }, cancellationToken));

    /// <summary>
    /// Lo descargado al recibir el camion, el kilometraje y la plata rendida, con todas
    /// las correcciones que se le hicieron despues.
    /// </summary>
    [HttpGet("{id:guid}/reception")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<ReceptionDto>> GetReception(Guid id, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetReceptionQuery(id), cancellationToken));

    /// <summary>
    /// Corrige la recepcion de una salida cerrada cuando se anoto mal algo. No pisa nada:
    /// suma movimientos y deja el antes y el despues con el motivo.
    /// </summary>
    [HttpPost("{id:guid}/reception-corrections")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<ReceptionDto>> CorrectReception(
        Guid id,
        CorrectReceptionCommand command,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(command with { Id = id }, cancellationToken));

    /// <summary>
    /// Anula una salida abierta sin movimientos (por ejemplo, la oficina la abrió y el chofer
    /// no vino). La carga vuelve a quedar pendiente en el camión. Solo ADMIN.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Cancel(Guid id, CancelSessionCommand command, CancellationToken cancellationToken)
    {
        await _mediator.Send(command with { Id = id }, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Recarga en ruta. Solo del admin: la carga cuando el chofer le avisa que se
    /// quedo sin stock, porque el equipo que acerca la mercaderia no usa el sistema.
    /// </summary>
    [HttpPost("{id:guid}/restocks")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<SessionRestockDto>> AddStock(
        Guid id,
        AddSessionStockCommand command,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(command with { Id = id }, cancellationToken));

    /// <summary>La sesion abierta del chofer que hace el request, con su stock a bordo.</summary>
    [HttpGet("current")]
    [Authorize(Roles = AppRoles.Driver)]
    public async Task<ActionResult<SessionDto?>> GetCurrent(CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetCurrentSessionQuery(), cancellationToken));

    /// <summary>Permite al chofer detectar la recepción de su salida sin recalcular el stock.</summary>
    [HttpGet("{id:guid}/status")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<SessionStatusDto>> GetStatus(Guid id, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetSessionStatusQuery(id), cancellationToken));

    /// <summary>Evita que venza el acceso durante un reparto activo. Requiere un JWT válido.</summary>
    [HttpPost("{id:guid}/keep-alive")]
    [Authorize(Roles = AppRoles.Driver)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<SessionHeartbeatDto>> KeepAlive(Guid id, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new KeepSessionAliveCommand(id), cancellationToken));

    /// <summary>
    /// Carga cuanta plata del chofer llego al admin. Se compara contra lo cobrado en
    /// la sesion, no contra lo vendido.
    /// </summary>
    [HttpPost("{id:guid}/settlement")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<SessionSettlementDto>> RegisterSettlement(
        Guid id,
        RegisterSettlementCommand command,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(command with { Id = id }, cancellationToken));

    /// <summary>Vendido, cobrado y entregado. Funciona tambien antes de rendir.</summary>
    [HttpGet("{id:guid}/settlement")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<SessionSettlementDto>> GetSettlement(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetSessionSettlementQuery(id), cancellationToken));

    /// <summary>
    /// Liquidacion detallada: por cliente, lo vendido, los envases devueltos, lo
    /// cobrado por medio de pago y el saldo de su cuenta.
    /// </summary>
    [HttpGet("{id:guid}/detailed-settlement")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<IReadOnlyList<CustomerSettlementDto>>> GetDetailedSettlement(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetSessionDetailedSettlementQuery(id), cancellationToken));

    /// <summary>
    /// El recorrido: las visitas de la salida en el orden en que se hicieron.
    /// </summary>
    [HttpGet("{id:guid}/unvisited-customers")]
    public async Task<ActionResult<UnvisitedCustomersDto>> GetUnvisited(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new GetUnvisitedCustomersQuery(id), ct));

    /// <summary>Lo que lleva vendido la salida: plata por medio de pago y unidades por producto.</summary>
    [HttpGet("{id:guid}/sales-summary")]
    public async Task<ActionResult<SessionSalesSummaryDto>> GetSalesSummary(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new GetSessionSalesSummaryQuery(id), ct));

    [HttpGet("{id:guid}/deliveries")]
    public async Task<ActionResult<IReadOnlyList<SessionDeliveryDto>>> GetDeliveries(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetSessionDeliveriesQuery(id), cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SessionDto>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetSessionByIdQuery(id), cancellationToken));

    [HttpGet]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<PagedResult<SessionDto>>> GetList(
        [FromQuery] GetSessionsQuery query,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(query, cancellationToken));

    [HttpGet("daily-summary")]
    [Authorize(Roles = AppRoles.Admin)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<DailySummaryDto>> GetDailySummary(
        [FromQuery] GetDailySummaryQuery query, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(query, cancellationToken));
}
