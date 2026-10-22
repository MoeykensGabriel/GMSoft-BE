using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.Reports.Common;
using GMSoft.Application.Features.Reports.ContainersOut;
using GMSoft.Application.Features.Reports.Debtors;
using GMSoft.Application.Features.Reports.InactiveCustomers;
using GMSoft.Application.Features.Reports.RouteSheet;
using MediatR;
using GMSoft.Application.Features.Reports.VehiclePerformance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GMSoft.API.Controllers;

/// <summary>Los numeros que mira el dueño. Todos del admin.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = AppRoles.Admin)]
public class ReportsController : ControllerBase
{
    private readonly ISender _mediator;

    public ReportsController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("vehicle-performance")]
    public async Task<ActionResult<VehiclePerformanceDto>> VehiclePerformance(
        [FromQuery] GetVehiclePerformanceQuery query, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(query, cancellationToken));

    /// <summary>
    /// Cuantos envases hay en la calle y entre cuantos clientes, por producto. Es la
    /// pregunta que justifica el sistema.
    /// </summary>
    [HttpGet("containers-out")]
    public async Task<ActionResult<IReadOnlyList<ContainersOutLineDto>>> ContainersOut(
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetContainersOutReportQuery(), cancellationToken));

    /// <summary>Quien debe, del que mas debe al que menos, con sus envases al lado.</summary>
    [HttpGet("debtors")]
    public async Task<ActionResult<PagedResult<DebtorLineDto>>> Debtors(
        [FromQuery] GetDebtorsReportQuery query,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(query, cancellationToken));

    /// <summary>
    /// Quien dejo de comprar, del que hace mas tiempo al que menos, con lo que debe y
    /// los envases que se quedo.
    /// </summary>
    [HttpGet("inactive-customers")]
    public async Task<ActionResult<PagedResult<InactiveCustomerLineDto>>> InactiveCustomers(
        [FromQuery] GetInactiveCustomersReportQuery query,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(query, cancellationToken));

    /// <summary>
    /// La hoja de ruta de un camión para una fecha: todos sus clientes de ese día, en
    /// orden de recorrido, con envases y saldo. Para imprimir.
    /// </summary>
    [HttpGet("route-sheet")]
    public async Task<ActionResult<RouteSheetDto>> RouteSheet(
        [FromQuery] GetRouteSheetQuery query,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(query, cancellationToken));
}
