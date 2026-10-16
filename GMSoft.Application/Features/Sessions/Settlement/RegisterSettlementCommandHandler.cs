using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using MediatR;
using GMSoft.Application.Features.Sessions.Common;
using GMSoft.Application.Features.WorkingHours;

namespace GMSoft.Application.Features.Sessions.Settlement;

public class RegisterSettlementCommandHandler
    : IRequestHandler<RegisterSettlementCommand, SessionSettlementDto>
{
    private readonly ISessionRepository _sessions;
    private readonly IRepository<SessionCashSettlement> _settlements;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly WorkingHoursService _workingHours;

    public RegisterSettlementCommandHandler(
        ISessionRepository sessions,
        IRepository<SessionCashSettlement> settlements,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        WorkingHoursService workingHours)
    {
        _sessions    = sessions;
        _settlements = settlements;
        _currentUser = currentUser;
        _unitOfWork  = unitOfWork;
        _workingHours = workingHours;
    }

    public async Task<SessionSettlementDto> Handle(
        RegisterSettlementCommand request,
        CancellationToken cancellationToken)
    {
        SessionCashSettlement settlement = null!;
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var session = await _sessions.LockAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException(nameof(DeliverySession), request.Id);

            // Rendir una sesion abierta no tiene sentido: el chofer sigue cobrando y la
            // comparacion cambiaria despues de haberla dado por buena.
            if (session.Status != SessionStatus.Closed)
                throw new ConflictException(
                    "La sesion todavia esta abierta. Se rinde cuando el chofer volvio y la cerro.");

            if (await _sessions.GetSettlementAsync(session.Id, cancellationToken) is not null)
                throw new ConflictException(
                    "Esta sesion ya fue rendida. Si el monto estaba mal, corregilo con «Corregir recepción» en la salida.");

            var horario = await _workingHours.EvaluateAsync(cancellationToken);
            settlement = new SessionCashSettlement
            {
                DeliverySessionId = session.Id,
                AmountReceived    = request.AmountReceived,
                ReceivedAt        = horario.UtcNow,
                IsOutsideWorkingHours = horario.IsOutsideWorkingHours,
                ReceivedByUserId  = _currentUser.UserId,
                Notes             = request.Notes?.Trim()
            };

            await _settlements.AddAsync(settlement, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        }, cancellationToken);

        var (vendido, cobrado) = await _sessions.GetMoneyTotalsAsync(request.Id, cancellationToken);

        var payments = await _sessions.GetPaymentsByCustomerAsync(request.Id, cancellationToken);
        var cash = payments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount);

        return new SessionSettlementDto(
            SessionId:      request.Id,
            TotalSold:      vendido,
            TotalCollected: cobrado,
            AmountReceived: settlement.AmountReceived,
            NewDebt:        vendido - cobrado,
            CashDifference: CashSettlementCalculation.Difference(cash, settlement.AmountReceived),
            ReceivedAt:     settlement.ReceivedAt,
            Notes:          settlement.Notes)
        { IsOutsideWorkingHours = settlement.IsOutsideWorkingHours };
    }
}
