using GMSoft.Application.Features.Zones.Common;
using MediatR;

namespace GMSoft.Application.Features.Zones.GetById;

public record GetZoneByIdQuery(Guid Id) : IRequest<ZoneDto>;
