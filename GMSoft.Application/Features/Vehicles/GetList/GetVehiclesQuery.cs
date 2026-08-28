using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.Vehicles.Common;
using MediatR;

namespace GMSoft.Application.Features.Vehicles.GetList;

public record GetVehiclesQuery(
    int     Page     = 1,
    int     PageSize = 20,
    string? Search   = null) : IRequest<PagedResult<VehicleDto>>;
