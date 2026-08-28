using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.Customers.Account;
using GMSoft.Application.Features.Customers.Common;
using GMSoft.Application.Features.Customers.Create;
using GMSoft.Application.Features.Customers.Delete;
using GMSoft.Application.Features.Customers.GetById;
using GMSoft.Application.Features.Customers.GetList;
using GMSoft.Application.Features.Customers.Update;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GMSoft.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = AppRoles.Admin)]
public class CustomersController : ControllerBase
{
    private readonly ISender _mediator;

    public CustomersController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Con zoneId es la hoja de ruta del chofer, en orden de recorrido. El chofer la
    /// lee para saber a quien visitar.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = AppRoles.AdminOrDriver)]
    public async Task<ActionResult<PagedResult<CustomerDto>>> GetList(
        [FromQuery] GetCustomersQuery query,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Roles = AppRoles.AdminOrDriver)]
    public async Task<ActionResult<CustomerDto>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetCustomerByIdQuery(id), cancellationToken));

    /// <summary>
    /// Cuanto debe y que envases tiene en su poder, con los ultimos movimientos.
    /// El chofer la lee para saber con que se encuentra en la puerta.
    /// </summary>
    [HttpGet("{id:guid}/account")]
    [Authorize(Roles = AppRoles.AdminOrDriver)]
    public async Task<ActionResult<CustomerAccountDto>> GetAccount(
        Guid id,
        [FromQuery] int movementsLimit,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(
            new GetCustomerAccountQuery(id, movementsLimit <= 0 ? 50 : movementsLimit),
            cancellationToken));

    /// <summary>
    /// Alta desde la oficina, sin venta. Solo del admin a proposito: el chofer da de
    /// alta un cliente unicamente cuando le vende algo, y eso va por el registro de
    /// la visita.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Guid>> Create(
        CreateCustomerCommand command,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateCustomerCommand command,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(command with { Id = id }, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteCustomerCommand(id), cancellationToken);
        return NoContent();
    }
}
