using GMSoft.Application.Common;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using MediatR;

namespace GMSoft.Application.Features.VehicleLoads.GetRemoved;

/// <summary>
/// Historial de lo que la oficina bajó de un camión antes de que saliera: qué, cuándo
/// y quién. Selecciona los últimos <see cref="Limit"/> renglones y los ordena por producto.
/// </summary>
public record GetRemovedVehicleLoadsQuery(Guid VehicleId) : IRequest<IReadOnlyList<RemovedVehicleLoadDto>>
{
    public const int Limit = 100;
}

/// <param name="RemovedByUserName">Null en lo bajado antes de que se registrara quién.</param>
public record RemovedVehicleLoadDto(
    Guid Id,
    string ProductDetail,
    int Quantity,
    DateTime LoadedAt,
    DateTime RemovedAt,
    string? RemovedByUserName);

public class GetRemovedVehicleLoadsQueryHandler(
    IVehicleLoadRepository loads,
    IIdentityService identity) : IRequestHandler<GetRemovedVehicleLoadsQuery, IReadOnlyList<RemovedVehicleLoadDto>>
{
    public async Task<IReadOnlyList<RemovedVehicleLoadDto>> Handle(
        GetRemovedVehicleLoadsQuery request, CancellationToken cancellationToken)
    {
        var removed = await loads.GetRemovedAsync(request.VehicleId, GetRemovedVehicleLoadsQuery.Limit, cancellationToken);
        var names = await identity.GetUserNamesAsync(
            removed.Where(l => l.RemovedByUserId.HasValue).Select(l => l.RemovedByUserId!.Value).Distinct().ToArray(),
            cancellationToken);

        return removed
            .Select(l => new RemovedVehicleLoadDto(l.Id, l.Product.Detail, l.Quantity, l.LoadedAt,
                l.DeletedAt ?? l.UpdatedAt,
                l.RemovedByUserId is Guid id && names.TryGetValue(id, out var name) ? name : null))
            .OrderByProduct(l => l.ProductDetail).ToList();
    }
}
