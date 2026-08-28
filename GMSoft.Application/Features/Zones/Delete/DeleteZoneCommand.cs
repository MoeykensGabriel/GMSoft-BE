using MediatR;

namespace GMSoft.Application.Features.Zones.Delete;

public record DeleteZoneCommand(Guid Id) : IRequest;
