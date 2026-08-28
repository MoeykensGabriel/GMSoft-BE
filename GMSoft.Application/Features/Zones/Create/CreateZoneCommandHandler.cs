using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.Zones.Create;

public class CreateZoneCommandHandler : IRequestHandler<CreateZoneCommand, Guid>
{
    private readonly IZoneRepository _zones;
    private readonly IUnitOfWork _unitOfWork;

    public CreateZoneCommandHandler(IZoneRepository zones, IUnitOfWork unitOfWork)
    {
        _zones      = zones;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateZoneCommand request, CancellationToken cancellationToken)
    {
        if (await _zones.ExistsByNameAsync(request.Name, null, cancellationToken))
            throw new ConflictException($"Ya existe una zona llamada '{request.Name}'.");

        var zone = new Zone
        {
            Name     = request.Name.Trim(),
            Notes    = request.Notes?.Trim(),
            IsActive = true
        };

        await _zones.AddAsync(zone, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return zone.Id;
    }
}
