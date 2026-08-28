using MediatR;

namespace GMSoft.Application.Features.Zones.Update;

public record UpdateZoneCommand(
    Guid    Id,
    string  Name,
    string? Notes,
    bool    IsActive) : IRequest;
