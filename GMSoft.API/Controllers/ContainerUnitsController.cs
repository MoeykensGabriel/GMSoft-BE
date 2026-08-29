using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.ContainerUnits.Assign;
using GMSoft.Application.Features.ContainerUnits.Common;
using GMSoft.Application.Features.ContainerUnits.Create;
using GMSoft.Application.Features.ContainerUnits.Decommission;
using GMSoft.Application.Features.ContainerUnits.GetById;
using GMSoft.Application.Features.ContainerUnits.GetList;
using GMSoft.Application.Features.ContainerUnits.Recover;
using GMSoft.Application.Features.ContainerUnits.Update;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GMSoft.API.Controllers;

/// <summary>
/// Envases identificados por numero de serie, tipicamente el dispenser. No se
/// mueven dentro de una visita: cada unidad se entrega y se recupera por separado,
/// porque lo que importa es cual esta donde y no cuantos hay.
/// </summary>
[ApiController]
[Route("api/container-units")]
[Authorize(Roles = AppRoles.AdminOrDriver)]
public class ContainerUnitsController : ControllerBase
{
    private readonly ISender _mediator;

    public ContainerUnitsController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Con status=WithCustomer es el listado de todo lo que esta en la calle.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<ContainerUnitDto>>> GetList(
        [FromQuery] GetContainerUnitsQuery query,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(query, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ContainerUnitDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetContainerUnitByIdQuery(id), cancellationToken));

    [HttpPost]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<Guid>> Create(
        CreateContainerUnitCommand command,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    /// <summary>Corrige el numero de serie. El estado se mueve con las acciones de abajo.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateContainerUnitCommand command,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(command with { Id = id }, cancellationToken);
        return NoContent();
    }

    /// <summary>Entrega la unidad a un cliente.</summary>
    [HttpPost("{id:guid}/assign")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Assign(
        Guid id,
        AssignContainerUnitCommand command,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(command with { Id = id }, cancellationToken);
        return NoContent();
    }

    /// <summary>El cliente la devuelve y vuelve al deposito.</summary>
    [HttpPost("{id:guid}/recover")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Recover(
        Guid id,
        RecoverContainerUnitCommand command,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(command with { Id = id }, cancellationToken);
        return NoContent();
    }

    /// <summary>Baja definitiva por rotura o perdida. El motivo es obligatorio.</summary>
    [HttpPost("{id:guid}/decommission")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Decommission(
        Guid id,
        DecommissionContainerUnitCommand command,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(command with { Id = id }, cancellationToken);
        return NoContent();
    }
}
