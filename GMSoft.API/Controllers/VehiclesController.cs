using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.Vehicles.Common;
using GMSoft.Application.Features.Vehicles.Create;
using GMSoft.Application.Features.Vehicles.Delete;
using GMSoft.Application.Features.Vehicles.GetById;
using GMSoft.Application.Features.Vehicles.GetList;
using GMSoft.Application.Features.Vehicles.Update;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GMSoft.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = AppRoles.Admin)]
public class VehiclesController : ControllerBase
{
    private readonly ISender _mediator;

    public VehiclesController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<VehicleDto>>> GetList(
        [FromQuery] GetVehiclesQuery query,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(query, cancellationToken));

    /// <summary>
    /// El chofer lo lee para ver el vehiculo que tiene asignado y su kilometraje
    /// al abrir la sesion.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = AppRoles.AdminOrDriver)]
    public async Task<ActionResult<VehicleDto>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetVehicleByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<Guid>> Create(
        CreateVehicleCommand command,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateVehicleCommand command,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(command with { Id = id }, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteVehicleCommand(id), cancellationToken);
        return NoContent();
    }
}
