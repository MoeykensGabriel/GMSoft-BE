using MediatR;

namespace GMSoft.Application.Features.VehicleLoads.Remove;

/// <summary>Bajar del camión una carga que todavía no salió.</summary>
public record RemoveVehicleLoadCommand(Guid VehicleId, Guid LoadId) : IRequest;
