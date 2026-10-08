using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Features.Customers.RoutePlanning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GMSoft.API.Controllers;

[ApiController]
[Route("api/route-planning")]
[Authorize(Roles = AppRoles.Admin)]
public class RoutePlanningController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<RoutePlanningDto>> Get([FromQuery] GetRoutePlanningQuery query, CancellationToken ct)
        => Ok(await mediator.Send(query, ct));

    [HttpPut]
    public async Task<ActionResult<RoutePlanningDto>> Save(SaveRoutePlanningCommand command, CancellationToken ct)
        => Ok(await mediator.Send(command, ct));
}
