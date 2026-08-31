using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.Vehicles.Common;
using GMSoft.Application.Features.Vehicles.Create;
using GMSoft.Application.Features.Vehicles.Delete;
using GMSoft.Application.Features.Vehicles.GetById;
using GMSoft.Application.Features.Vehicles.GetList;
using GMSoft.Application.Features.Vehicles.LoadStatus;
using GMSoft.Application.Features.Vehicles.Update;
using GMSoft.Application.Features.VehicleLoads.Common;
using GMSoft.Application.Features.VehicleLoads.GetPending;
using GMSoft.Application.Features.VehicleLoads.Register;
using GMSoft.Application.Features.VehicleLoads.Remove;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GMSoft.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = AppRoles.AdminOrDriver)]
public class VehiclesController : ControllerBase
{
    private readonly ISender _mediator;

    public VehiclesController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<PagedResult<VehicleDto>>> GetList(
        [FromQuery] GetVehiclesQuery query,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(query, cancellationToken));

    /// <summary>
    /// El chofer lo lee para ver el vehiculo que tiene asignado y su kilometraje
    /// al abrir la sesion.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<VehicleDto>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetVehicleByIdQuery(id), cancellationToken));

    [HttpPost]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<Guid>> Create(
        CreateVehicleCommand command,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateVehicleCommand command,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(command with { Id = id }, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteVehicleCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// La flota con su estado de carga: cual esta en la calle y cual ya tiene
    /// mercaderia arriba. Es lo que decide que camiones se pueden cargar.
    /// </summary>
    [HttpGet("load-status")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<IReadOnlyList<VehicleLoadStatusDto>>> GetLoadStatus(
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetVehiclesLoadStatusQuery(), cancellationToken));

    /// <summary>
    /// Lo que el camion tiene cargado y todavia no salio. El chofer tambien lo lee:
    /// al abrir la salida confirma que es lo que ve arriba del camion.
    /// </summary>
    [HttpGet("{id:guid}/load")]
    public async Task<ActionResult<IReadOnlyList<VehicleLoadLineDto>>> GetPendingLoad(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetPendingVehicleLoadQuery(id), cancellationToken));

    /// <summary>
    /// La oficina sube mercaderia al camion antes de que salga. Se rechaza si el
    /// camion esta en la calle: eso es una recarga en ruta sobre la salida abierta.
    /// </summary>
    [HttpPost("{id:guid}/load")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> RegisterLoad(
        Guid id,
        RegisterVehicleLoadCommand command,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(command with { VehicleId = id }, cancellationToken);
        return NoContent();
    }

    /// <summary>Baja del camion una carga que todavia no salio.</summary>
    [HttpDelete("{id:guid}/load/{loadId:guid}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> RemoveLoad(
        Guid id,
        Guid loadId,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(new RemoveVehicleLoadCommand(id, loadId), cancellationToken);
        return NoContent();
    }
}
