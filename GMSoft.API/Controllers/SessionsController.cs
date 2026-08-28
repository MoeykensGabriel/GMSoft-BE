using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.Sessions.AddStock;
using GMSoft.Application.Features.Sessions.Close;
using GMSoft.Application.Features.Sessions.Common;
using GMSoft.Application.Features.Sessions.GetById;
using GMSoft.Application.Features.Sessions.GetCurrent;
using GMSoft.Application.Features.Sessions.GetList;
using GMSoft.Application.Features.Sessions.Open;
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

    public SessionsController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Abre la salida: kilometraje, zona y lo que sube al camion. El vehiculo sale
    /// de la asignacion del chofer.
    /// </summary>
    [HttpPost("open")]
    [Authorize(Roles = AppRoles.Driver)]
    public async Task<ActionResult<Guid>> Open(
        OpenSessionCommand command,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    /// <summary>
    /// Cierra la salida con el kilometraje de vuelta y lo que se descarga. Devuelve
    /// el faltante, si lo hay.
    /// </summary>
    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<CloseSessionResult>> Close(
        Guid id,
        CloseSessionCommand command,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(command with { Id = id }, cancellationToken));

    /// <summary>
    /// Recarga en ruta. Solo del admin: la carga cuando el chofer le avisa que se
    /// quedo sin stock, porque el equipo que acerca la mercaderia no usa el sistema.
    /// </summary>
    [HttpPost("{id:guid}/stock")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> AddStock(
        Guid id,
        AddSessionStockCommand command,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(command with { Id = id }, cancellationToken);
        return NoContent();
    }

    /// <summary>La sesion abierta del chofer que hace el request, con su stock a bordo.</summary>
    [HttpGet("current")]
    [Authorize(Roles = AppRoles.Driver)]
    public async Task<ActionResult<SessionDto?>> GetCurrent(CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetCurrentSessionQuery(), cancellationToken));

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

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SessionDto>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetSessionByIdQuery(id), cancellationToken));

    [HttpGet]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<PagedResult<SessionDto>>> GetList(
        [FromQuery] GetSessionsQuery query,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(query, cancellationToken));
}
