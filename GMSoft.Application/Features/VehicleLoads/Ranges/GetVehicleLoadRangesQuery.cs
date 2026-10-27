using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.VehicleLoads.Ranges;

public record GetVehicleLoadRangesQuery(Guid VehicleId) : IRequest<IReadOnlyList<VehicleLoadRangeDto>>;

public class GetVehicleLoadRangesQueryHandler(
    IVehicleLoadRangeRepository ranges, IVehicleRepository vehicles, ICurrentUserService user)
    : IRequestHandler<GetVehicleLoadRangesQuery, IReadOnlyList<VehicleLoadRangeDto>>
{
    public async Task<IReadOnlyList<VehicleLoadRangeDto>> Handle(
        GetVehicleLoadRangesQuery request, CancellationToken cancellationToken)
    {
        if (!user.IsInRole(AppRoles.Admin))
            throw new ForbiddenException("Solo el administrador puede consultar los rangos de carga.");
        if (!await vehicles.ExistsAsync(request.VehicleId, cancellationToken))
            throw new NotFoundException(nameof(Vehicle), request.VehicleId);
        return await ranges.GetAsync(request.VehicleId, cancellationToken);
    }
}
