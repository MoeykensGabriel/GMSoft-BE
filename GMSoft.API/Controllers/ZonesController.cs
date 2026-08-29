using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.Zones.Common;
using GMSoft.Application.Features.Zones.Create;
using GMSoft.Application.Features.Zones.Delete;
using GMSoft.Application.Features.Zones.GetById;
using GMSoft.Application.Features.Zones.GetList;
using GMSoft.Application.Features.Zones.Update;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GMSoft.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = AppRoles.AdminOrDriver)]
public class ZonesController : ControllerBase
{
    private readonly ISender _mediator;

    public ZonesController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// El chofer tambien lo lee: es la lista que elige al abrir la sesion de reparto.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<ZoneDto>>> GetList(
        [FromQuery] GetZonesQuery query,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(query, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ZoneDto>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetZoneByIdQuery(id), cancellationToken));

    [HttpPost]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<Guid>> Create(
        CreateZoneCommand command,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateZoneCommand command,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(command with { Id = id }, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteZoneCommand(id), cancellationToken);
        return NoContent();
    }
}
