using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using MediatR;

namespace GMSoft.Application.Features.Sessions.DetailedSettlement;

public class GetSessionDetailedSettlementQueryHandler
    : IRequestHandler<GetSessionDetailedSettlementQuery, IReadOnlyList<CustomerSettlementDto>>
{
    private readonly ISessionRepository _sessions;

    public GetSessionDetailedSettlementQueryHandler(ISessionRepository sessions)
    {
        _sessions = sessions;
    }

    public async Task<IReadOnlyList<CustomerSettlementDto>> Handle(
        GetSessionDetailedSettlementQuery request,
        CancellationToken cancellationToken)
    {
        var session = await _sessions.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(DeliverySession), request.Id);

        var deliveries = await _sessions.GetDeliveriesAsync(session.Id, cancellationToken);
        var payments   = await _sessions.GetPaymentsByCustomerAsync(session.Id, cancellationToken);
        // El saldo se corta al cierre: mirar una salida vieja no tiene que mostrar
        // la deuda de hoy.
        var balances   = await _sessions.GetCustomerBalancesAsync(
            session.Id, session.ClosedAt ?? DateTime.UtcNow, cancellationToken);

        decimal Paid(Guid customerId, PaymentMethod method)
            => payments.Where(p => p.CustomerId == customerId && p.Method == method).Sum(p => p.Amount);

        // Las visitas ya vienen por hora: agrupar conserva el orden del recorrido y
        // junta en un solo bloque al cliente que se visito dos veces.
        return deliveries
            .GroupBy(d => d.CustomerId)
            .Select(visits =>
            {
                var first = visits.First();

                return new CustomerSettlementDto(
                    CustomerId:      first.CustomerId,
                    CustomerName:    first.CustomerName,
                    CustomerAddress: first.CustomerAddress,
                    CustomerPhone:   first.CustomerPhone,
                    Lines: visits
                        .SelectMany(d => d.Items.Select(i => new CustomerSettlementLineDto(
                            d.DeliveredAt, d.Type, i.Quantity, i.ProductDetail, i.Quantity * i.UnitPrice)))
                        .ToList(),
                    ReturnedContainers: visits
                        .SelectMany(d => d.Containers)
                        .Where(c => c.Quantity < 0)
                        .GroupBy(c => c.ProductId)
                        .Select(g => new CustomerReturnedContainerDto(g.First().ProductDetail, -g.Sum(c => c.Quantity)))
                        .ToList(),
                    Cash:     Paid(first.CustomerId, PaymentMethod.Cash),
                    Transfer: Paid(first.CustomerId, PaymentMethod.Transfer),
                    Card:     Paid(first.CustomerId, PaymentMethod.Card),
                    Balance:  balances.TryGetValue(first.CustomerId, out var balance) ? balance : 0m);
            })
            .ToList();
    }
}
