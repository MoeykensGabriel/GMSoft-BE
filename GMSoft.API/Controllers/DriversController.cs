using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.Drivers.Common;
using GMSoft.Application.Features.Drivers.Create;
using GMSoft.Application.Features.Drivers.Delete;
using GMSoft.Application.Features.Drivers.GetById;
using GMSoft.Application.Features.Drivers.GetMe;
using GMSoft.Application.Features.Drivers.GetList;
using GMSoft.Application.Features.Drivers.ResetPassword;
using GMSoft.Application.Features.Drivers.Update;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GMSoft.API.Controllers;

/// <summary>
/// Choferes. Todo el ABM es del admin: crear un chofer es crearle la cuenta con la
/// que entra al sistema, asi que nadie mas puede tocarlo.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = AppRoles.AdminOrDriver)]
public class DriversController : ControllerBase
{
    private readonly ISender _mediator;

    public DriversController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// El propio perfil del chofer, con el vehiculo que tiene asignado. Necesita
    /// verlo antes de salir; el resto del ABM sigue siendo solo del admin.
    /// </summary>
    [HttpGet("me")]
    [Authorize(Roles = AppRoles.Driver)]
    public async Task<ActionResult<DriverDto>> GetMe(CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetMyDriverProfileQuery(), cancellationToken));

    [HttpGet]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<PagedResult<DriverDto>>> GetList(
        [FromQuery] GetDriversQuery query,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<DriverDto>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetDriverByIdQuery(id), cancellationToken));

    /// <summary>
    /// Da de alta al chofer y su cuenta en una sola operacion: el admin le asigna
    /// el usuario y la contraseña en el momento.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<Guid>> Create(
        CreateDriverCommand command,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateDriverCommand command,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(command with { Id = id }, cancellationToken);
        return NoContent();
    }

    /// <summary>El admin le asigna una contraseña nueva sin pedir la anterior.</summary>
    [HttpPut("{id:guid}/password")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> ResetPassword(
        Guid id,
        ResetDriverPasswordCommand command,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(command with { Id = id }, cancellationToken);
        return NoContent();
    }

    /// <summary>Da de baja la ficha y le cierra el acceso al sistema.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteDriverCommand(id), cancellationToken);
        return NoContent();
    }
}
