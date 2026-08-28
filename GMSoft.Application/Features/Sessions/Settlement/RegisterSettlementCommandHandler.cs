using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using MediatR;

namespace GMSoft.Application.Features.Sessions.Settlement;

public class RegisterSettlementCommandHandler
    : IRequestHandler<RegisterSettlementCommand, SessionSettlementDto>
{
    private readonly ISessionRepository _sessions;
    private readonly IRepository<SessionCashSettlement> _settlements;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterSettlementCommandHandler(
        ISessionRepository sessions,
        IRepository<SessionCashSettlement> settlements,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _sessions    = sessions;
        _settlements = settlements;
        _currentUser = currentUser;
        _unitOfWork  = unitOfWork;
    }

    public async Task<SessionSettlementDto> Handle(
        RegisterSettlementCommand request,
        CancellationToken cancellationToken)
    {
        var session = await _sessions.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(DeliverySession), request.Id);

        // Rendir una sesion abierta no tiene sentido: el chofer sigue cobrando y la
        // comparacion cambiaria despues de haberla dado por buena.
        if (session.Status != SessionStatus.Closed)
            throw new ConflictException(
                "La sesion todavia esta abierta. Se rinde cuando el chofer volvio y la cerro.");

        if (await _sessions.GetSettlementAsync(session.Id, cancellationToken) is not null)
            throw new ConflictException(
                "Esta sesion ya fue rendida. Si el monto estaba mal, corregilo desde la rendicion existente.");

        var settlement = new SessionCashSettlement
        {
            DeliverySessionId = session.Id,
            AmountReceived    = request.AmountReceived,
            ReceivedAt        = DateTime.UtcNow,
            ReceivedByUserId  = _currentUser.UserId,
            Notes             = request.Notes?.Trim()
        };

        await _settlements.AddAsync(settlement, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var (vendido, cobrado) = await _sessions.GetMoneyTotalsAsync(session.Id, cancellationToken);

        return new SessionSettlementDto(
            SessionId:      session.Id,
            TotalSold:      vendido,
            TotalCollected: cobrado,
            AmountReceived: settlement.AmountReceived,
            NewDebt:        vendido - cobrado,
            CashDifference: cobrado - settlement.AmountReceived,
            ReceivedAt:     settlement.ReceivedAt,
            Notes:          settlement.Notes);
    }
}
