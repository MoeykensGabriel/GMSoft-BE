using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Features.Deliveries.Register;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GMSoft.API.Controllers;

/// <summary>Visitas al cliente dentro de una sesion de reparto.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = AppRoles.Driver)]
public class DeliveriesController : ControllerBase
{
    private readonly ISender _mediator;

    public DeliveriesController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Registra la visita: venta con el precio del cliente, movimiento de envases en
    /// las dos direcciones y cobro si lo hubo, todo en una transaccion. La sesion es
    /// la que el chofer tiene abierta.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<RegisterDeliveryResult>> Register(
        RegisterDeliveryCommand command,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(command, cancellationToken));
}
