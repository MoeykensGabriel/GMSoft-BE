using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Features.Inspections;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GMSoft.API.Controllers;

/// <summary>Control en ruta: el controlador sube a un camión, cuenta envases y confirma clientes.</summary>
[ApiController]
[Route("api/inspections")]
[Authorize(Roles = AppRoles.Inspector)]
public class InspectionsController(ISender mediator) : ControllerBase
{
    /// <summary>Los camiones que están en la calle, para elegir a cuál subirse.</summary>
    [HttpGet("departures")]
    public async Task<ActionResult<IReadOnlyList<InspectionDepartureDto>>> Departures(CancellationToken ct)
        => Ok(await mediator.Send(new GetInspectionDeparturesQuery(), ct));

    /// <summary>Los clientes de esa salida en orden de recorrido, con sus envases y lo que marcó el chofer.</summary>
    [HttpGet("departures/{sessionId:guid}")]
    public async Task<ActionResult<InspectionRouteDto>> Route(Guid sessionId, CancellationToken ct)
        => Ok(await mediator.Send(new GetInspectionRouteQuery(sessionId), ct));

    /// <summary>Guarda el control de un cliente y corrige sus envases en el momento.</summary>
    [HttpPut("departures/{sessionId:guid}/customers/{customerId:guid}")]
    public async Task<ActionResult<InspectionCustomerDto>> Save(Guid sessionId, Guid customerId,
        SaveCustomerInspectionBody body, CancellationToken ct)
        => Ok(await mediator.Send(new SaveCustomerInspectionCommand(sessionId, customerId, body.Containers,
            body.StillBuys, body.NotBuyingReason, body.Notes), ct));
}

public record SaveCustomerInspectionBody(InspectionCountInput[] Containers, bool StillBuys,
    GMSoft.Domain.Entities.NotBuyingReason? NotBuyingReason, string? Notes);
