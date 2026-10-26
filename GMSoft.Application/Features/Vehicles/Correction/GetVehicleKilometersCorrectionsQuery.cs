using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.Vehicles.Correction;

public record VehicleKilometersCorrectionDto(DateTime CorrectedAt, int PreviousKilometers,
    int NewKilometers, string Reason, string? CorrectedByName);

public record GetVehicleKilometersCorrectionsQuery(Guid Id) : IRequest<IReadOnlyList<VehicleKilometersCorrectionDto>>;

public class GetVehicleKilometersCorrectionsQueryHandler(
    IVehicleRepository vehicles, IIdentityService identity, ICurrentUserService currentUser)
    : IRequestHandler<GetVehicleKilometersCorrectionsQuery, IReadOnlyList<VehicleKilometersCorrectionDto>>
{
    public async Task<IReadOnlyList<VehicleKilometersCorrectionDto>> Handle(
        GetVehicleKilometersCorrectionsQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsInRole(AppRoles.Admin))
            throw new ForbiddenException("El historial lo ve la oficina.");
        if (!await vehicles.ExistsAsync(request.Id, cancellationToken))
            throw new NotFoundException(nameof(Vehicle), request.Id);
        var history = await vehicles.GetKilometersCorrectionsAsync(request.Id, cancellationToken);
        var names = await identity.GetUserNamesAsync(history.Where(c => c.CorrectedByUserId.HasValue)
            .Select(c => c.CorrectedByUserId!.Value).Distinct().ToArray(), cancellationToken);
        return history.Select(c => new VehicleKilometersCorrectionDto(c.CorrectedAt,
            c.PreviousKilometers, c.NewKilometers, c.Reason,
            c.CorrectedByUserId is Guid id && names.TryGetValue(id, out var name) ? name : null)).ToList();
    }
}
