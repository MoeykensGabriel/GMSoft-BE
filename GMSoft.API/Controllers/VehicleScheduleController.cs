using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Features.Vehicles.Schedule;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GMSoft.API.Controllers;

[ApiController]
[Authorize(Roles = AppRoles.AdminOrDriver)]
public class VehicleScheduleController(ISender mediator) : ControllerBase
{
    [HttpGet("api/vehicles/{id:guid}/schedule")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await mediator.Send(new GetVehicleScheduleQuery(id), ct));

    public record SaveWeek(ScheduleDayInput[] Days);
    [HttpPut("api/vehicles/{id:guid}/schedule")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Save(Guid id, SaveWeek body, CancellationToken ct) =>
        Ok(await mediator.Send(new SaveVehicleScheduleCommand(id, body.Days), ct));

    [HttpGet("api/sessions/suggested-zone")]
    public async Task<IActionResult> Suggest([FromQuery] Guid? driverId, CancellationToken ct) =>
        Ok(await mediator.Send(new SuggestedZoneQuery(driverId), ct));

    [HttpPost("api/route-planning/coverage")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Coverage(CheckCoverageQuery body, CancellationToken ct) => Ok(await mediator.Send(body, ct));

    [HttpGet("api/route-planning/uncovered")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Uncovered([FromQuery] GetUncoveredQuery query, CancellationToken ct) => Ok(await mediator.Send(query, ct));
}
