using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.Deliveries.Register;
using GMSoft.Application.Features.Promotions.Close;
using GMSoft.Application.Features.Promotions.Common;
using GMSoft.Application.Features.Promotions.GetById;
using GMSoft.Application.Features.Promotions.GetSettings;
using GMSoft.Application.Features.Promotions.List;
using GMSoft.Application.Features.Promotions.Pending;
using GMSoft.Application.Features.Promotions.Register;
using GMSoft.Application.Features.Promotions.UpdateSettings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GMSoft.API.Controllers;

[ApiController]
[Route("api/promotions")]
[Authorize]
public class PromotionsController(ISender mediator) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = AppRoles.Driver)]
    public async Task<ActionResult<PromotionResult>> Register(RegisterPromotionCommand body, CancellationToken ct) =>
        Ok(await mediator.Send(body, ct));

    [HttpGet("pending")]
    [Authorize(Roles = AppRoles.Driver)]
    public async Task<ActionResult<IReadOnlyList<PromotionDto>>> Pending(CancellationToken ct) =>
        Ok(await mediator.Send(new GetPendingPromotionsQuery(), ct));

    [HttpPost("{id:guid}/close")]
    [Authorize(Roles = AppRoles.Driver)]
    public async Task<ActionResult<PromotionResult>> Close(Guid id, ClosePromotionBody body, CancellationToken ct) =>
        Ok(await mediator.Send(new ClosePromotionCommand(id, body.ClientRequestId, body.ConvertToCustomer,
            body.Customer, body.ContainersReturned), ct));

    [HttpGet]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<PagedResult<PromotionDto>>> List([FromQuery] GetPromotionsQuery query, CancellationToken ct) =>
        Ok(await mediator.Send(query, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<PromotionDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetPromotionQuery(id), ct));

    [HttpGet("settings")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<PromotionSettingsDto>> Settings(CancellationToken ct) =>
        Ok(await mediator.Send(new GetPromotionSettingsQuery(), ct));

    [HttpPut("settings")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<PromotionSettingsDto>> UpdateSettings(UpdatePromotionSettingsCommand body, CancellationToken ct) =>
        Ok(await mediator.Send(body, ct));
}

public record ClosePromotionBody(Guid ClientRequestId, bool ConvertToCustomer,
    NewCustomerLine? Customer, IReadOnlyList<ContainerLine> ContainersReturned);

