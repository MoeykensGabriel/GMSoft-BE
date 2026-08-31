using MediatR;

namespace GMSoft.Application.Features.Vehicles.LoadStatus;

/// <summary>
/// Todos los vehículos con su estado de carga. Sin paginar: es una flota, y quien
/// lo llama necesita la lista entera para armar el selector.
/// </summary>
public record GetVehiclesLoadStatusQuery : IRequest<IReadOnlyList<VehicleLoadStatusDto>>;
