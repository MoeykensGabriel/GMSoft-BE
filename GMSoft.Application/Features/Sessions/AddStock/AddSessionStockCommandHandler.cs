using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using MediatR;

namespace GMSoft.Application.Features.Sessions.AddStock;

public class AddSessionStockCommandHandler : IRequestHandler<AddSessionStockCommand>
{
    private readonly ISessionRepository _sessions;
    private readonly IRepository<Product> _products;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public AddSessionStockCommandHandler(
        ISessionRepository sessions,
        IRepository<Product> products,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _sessions    = sessions;
        _products    = products;
        _currentUser = currentUser;
        _unitOfWork  = unitOfWork;
    }

    public async Task Handle(AddSessionStockCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessions.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(DeliverySession), request.Id);

        // Cargar una recarga sobre una sesion ya cerrada le mueve el faltante que el
        // admin ya vio. Si hubo un error, se corrige con un ajuste, no con una recarga.
        if (session.Status == SessionStatus.Closed)
            throw new ConflictException(
                "La sesion ya esta cerrada. Una recarga posterior cambiaria el faltante ya informado.");

        if (!await _products.ExistsAsync(request.ProductId, cancellationToken))
            throw new NotFoundException(nameof(Product), request.ProductId);

        session.StockMovements.Add(new SessionStockMovement
        {
            DeliverySessionId  = session.Id,
            ProductId          = request.ProductId,
            State              = ContainerState.Full,
            Quantity           = request.Quantity,
            Type               = SessionStockMovementType.Restock,
            OccurredAt         = DateTime.UtcNow,
            RegisteredByUserId = _currentUser.UserId,
            Notes              = request.Notes?.Trim()
        });

        _sessions.Update(session);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
