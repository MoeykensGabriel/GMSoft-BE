using MediatR;

namespace GMSoft.Application.Features.Vehicles.Delete;

public record DeleteVehicleCommand(Guid Id) : IRequest;
