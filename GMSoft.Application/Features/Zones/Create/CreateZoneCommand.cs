using MediatR;

namespace GMSoft.Application.Features.Zones.Create;

public record CreateZoneCommand(string Name, string? Notes) : IRequest<Guid>;
