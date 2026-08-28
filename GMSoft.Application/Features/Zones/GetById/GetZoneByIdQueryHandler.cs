using Mapster;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Zones.Common;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.Zones.GetById;

public class GetZoneByIdQueryHandler : IRequestHandler<GetZoneByIdQuery, ZoneDto>
{
    private readonly IZoneRepository _zones;

    public GetZoneByIdQueryHandler(IZoneRepository zones)
    {
        _zones = zones;
    }

    public async Task<ZoneDto> Handle(GetZoneByIdQuery request, CancellationToken cancellationToken)
    {
        var zone = await _zones.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Zone), request.Id);

        return zone.Adapt<ZoneDto>();
    }
}
