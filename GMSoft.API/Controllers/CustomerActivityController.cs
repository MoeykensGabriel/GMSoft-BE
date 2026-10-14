using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Features.Customers.ActivitySettings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GMSoft.API.Controllers;

/// <summary>Umbrales de actividad de clientes: cuántas semanas sin comprar pasan a rojo y a negro.</summary>
[ApiController]
[Route("api/customer-activity/settings")]
[Authorize(Roles = AppRoles.Admin)]
public class CustomerActivityController(ISender mediator) : ControllerBase
{
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<CustomerActivitySettingsDto>> Get(CancellationToken ct) =>
        Ok(await mediator.Send(new GetCustomerActivitySettingsQuery(), ct));

    [HttpPut]
    public async Task<ActionResult<CustomerActivitySettingsDto>> Put(UpdateCustomerActivitySettingsCommand command, CancellationToken ct) =>
        Ok(await mediator.Send(command, ct));
}
