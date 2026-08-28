using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.Zones.Update;

public class UpdateZoneCommandHandler : IRequestHandler<UpdateZoneCommand>
{
    private readonly IZoneRepository _zones;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateZoneCommandHandler(IZoneRepository zones, IUnitOfWork unitOfWork)
    {
        _zones      = zones;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateZoneCommand request, CancellationToken cancellationToken)
    {
        var zone = await _zones.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Zone), request.Id);

        if (await _zones.ExistsByNameAsync(request.Name, request.Id, cancellationToken))
            throw new ConflictException($"Ya existe otra zona llamada '{request.Name}'.");

        zone.Name     = request.Name.Trim();
        zone.Notes    = request.Notes?.Trim();
        zone.IsActive = request.IsActive;

        _zones.Update(zone);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
