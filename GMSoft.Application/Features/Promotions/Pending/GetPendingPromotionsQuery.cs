using GMSoft.Application.Common;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Customers.Common;
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
        // Cada promocion es del dia de recorrido en que se registro: el chofer ve solo las
        // de los dias de su salida, o las de hoy si no salio.
        var days = session is null ? [BusinessTime.IsoDayOfWeek(DateTime.UtcNow)] : CustomerRouteAccess.Days(session);
        return (await promotions.GetPendingAsync(vehicleId, days, ct)).Select(PromotionDto.From).ToList();
    }
}

