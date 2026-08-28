using Mapster;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.Vehicles.Common;
using MediatR;

namespace GMSoft.Application.Features.Vehicles.GetList;

public class GetVehiclesQueryHandler : IRequestHandler<GetVehiclesQuery, PagedResult<VehicleDto>>
{
    private readonly IVehicleRepository _vehicles;

    public GetVehiclesQueryHandler(IVehicleRepository vehicles)
    {
        _vehicles = vehicles;
    }

    public async Task<PagedResult<VehicleDto>> Handle(
        GetVehiclesQuery request,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _vehicles.GetPagedAsync(
            request.Page, request.PageSize, request.Search, cancellationToken);

        return new PagedResult<VehicleDto>(
            items.Adapt<List<VehicleDto>>(), totalCount, request.Page, request.PageSize);
    }
}
