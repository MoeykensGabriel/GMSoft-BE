using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.Zones.Delete;

public class DeleteZoneCommandHandler : IRequestHandler<DeleteZoneCommand>
{
    private readonly IZoneRepository _zones;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteZoneCommandHandler(IZoneRepository zones, IUnitOfWork unitOfWork)
    {
        _zones      = zones;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteZoneCommand request, CancellationToken cancellationToken)
    {
        var zone = await _zones.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Zone), request.Id);

        // Una zona con clientes o sesiones no se elimina: dejaria clientes sin
        // recorrido y salidas historicas sin poder decir de donde salieron.
        if (await _zones.HasHistoryAsync(request.Id, cancellationToken))
            throw new ConflictException(
                "Esta zona ya tiene clientes o sesiones y no se puede eliminar. " +
                "Desactivala: deja de ofrecerse al abrir una sesion y el historial queda intacto.");

        _zones.Delete(zone);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
