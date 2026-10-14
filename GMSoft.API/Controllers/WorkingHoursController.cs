using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Features.WorkingHours;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GMSoft.API.Controllers;

[ApiController]
[Route("api/working-hours")]
[Authorize]
public class WorkingHoursController(ISender mediator) : ControllerBase
{
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<WorkingHoursDto>> Get(CancellationToken ct) =>
        Ok(await mediator.Send(new GetWorkingHoursQuery(), ct));

    [HttpPut]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<WorkingHoursDto>> Put(UpdateWorkingHoursCommand command, CancellationToken ct) =>
        Ok(await mediator.Send(command, ct));
}
