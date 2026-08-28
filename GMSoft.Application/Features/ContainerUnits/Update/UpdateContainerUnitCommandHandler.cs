using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.ContainerUnits.Update;

public class UpdateContainerUnitCommandHandler : IRequestHandler<UpdateContainerUnitCommand>
{
    private readonly IContainerUnitRepository _units;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateContainerUnitCommandHandler(IContainerUnitRepository units, IUnitOfWork unitOfWork)
    {
        _units      = units;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateContainerUnitCommand request, CancellationToken cancellationToken)
    {
        var unit = await _units.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ContainerUnit), request.Id);

        var serie = request.SerialNumber.Trim();

        if (await _units.ExistsBySerialNumberAsync(serie, request.Id, cancellationToken))
            throw new ConflictException($"Ya existe otra unidad con el numero {serie}.");

        unit.SerialNumber = serie;

        _units.Update(unit);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
