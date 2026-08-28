using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.Sessions.Settlement;

public class GetSessionSettlementQueryHandler
    : IRequestHandler<GetSessionSettlementQuery, SessionSettlementDto>
{
    private readonly ISessionRepository _sessions;

    public GetSessionSettlementQueryHandler(ISessionRepository sessions)
    {
        _sessions = sessions;
    }

    public async Task<SessionSettlementDto> Handle(
        GetSessionSettlementQuery request,
        CancellationToken cancellationToken)
    {
        var session = await _sessions.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(DeliverySession), request.Id);

        var (vendido, cobrado) = await _sessions.GetMoneyTotalsAsync(session.Id, cancellationToken);
        var settlement         = await _sessions.GetSettlementAsync(session.Id, cancellationToken);

        return new SessionSettlementDto(
            SessionId:      session.Id,
            TotalSold:      vendido,
            TotalCollected: cobrado,
            AmountReceived: settlement?.AmountReceived,
            NewDebt:        vendido - cobrado,
            // Sin rendicion no hay diferencia que informar: cero seria mentira, porque
            // significaria que cuadra.
            CashDifference: settlement is null ? null : cobrado - settlement.AmountReceived,
            ReceivedAt:     settlement?.ReceivedAt,
            Notes:          settlement?.Notes);
    }
}
