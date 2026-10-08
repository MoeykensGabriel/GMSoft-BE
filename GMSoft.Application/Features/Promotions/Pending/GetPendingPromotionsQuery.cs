using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Promotions.Common;
using MediatR;

namespace GMSoft.Application.Features.Promotions.Pending;

public record GetPendingPromotionsQuery : IRequest<IReadOnlyList<PromotionDto>>;
public class GetPendingPromotionsQueryHandler(IPromotionRepository promotions, ISessionRepository sessions,
    IDriverRepository drivers, ICurrentUserService user)
    : IRequestHandler<GetPendingPromotionsQuery, IReadOnlyList<PromotionDto>>
{
    public async Task<IReadOnlyList<PromotionDto>> Handle(GetPendingPromotionsQuery request, CancellationToken ct)
    {
        var driverId = user.DriverId ?? throw new ForbiddenException("Solo un chofer consulta sus promociones.");
        var session = await sessions.GetOpenByDriverAsync(driverId, ct);
        var vehicleId = session?.VehicleId ?? (await drivers.GetByIdAsync(driverId, ct))?.VehicleId
            ?? throw new ConflictException("No tenes vehiculo asignado.");
        return (await promotions.GetPendingAsync(vehicleId, ct)).Select(PromotionDto.From).ToList();
    }
}

