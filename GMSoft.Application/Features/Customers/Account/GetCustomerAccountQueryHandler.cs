using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.Customers.Account;

public class GetCustomerAccountQueryHandler
    : IRequestHandler<GetCustomerAccountQuery, CustomerAccountDto>
{
    private readonly ICustomerRepository _customers;
    private readonly IContainerBalanceRepository _balances;
    private readonly IContainerUnitRepository _units;

    public GetCustomerAccountQueryHandler(
        ICustomerRepository customers,
        IContainerBalanceRepository balances,
        IContainerUnitRepository units)
    {
        _customers = customers;
        _balances  = balances;
        _units     = units;
    }

    public async Task<CustomerAccountDto> Handle(
        GetCustomerAccountQuery request,
        CancellationToken cancellationToken)
    {
        var customer = await _customers.GetWithZoneAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.Id);

        var balance   = await _customers.GetAccountBalanceAsync(customer.Id, cancellationToken);
        var movements = await _customers.GetAccountMovementsAsync(
            customer.Id, request.MovementsLimit, cancellationToken);

        var ultimasCompras = await _customers.GetLastPurchaseDatesAsync([customer.Id], cancellationToken);
        var ultimaCompra   = ultimasCompras.TryGetValue(customer.Id, out var fecha) ? fecha : (DateTime?)null;

        var saldosEnvases = await _balances.GetByCustomerAsync(customer.Id, cancellationToken);

        var envases = saldosEnvases
            // Un saldo en cero no es un envase en la calle: solo ensucia la pantalla.
            .Where(b => b.Quantity != 0)
            .Select(b => new CustomerContainerLineDto(
                b.ProductId,
                b.Product?.Detail ?? string.Empty,
                b.Quantity))
            .ToList();

        var unidades = (await _units.GetByCustomerAsync(customer.Id, cancellationToken))
            .Select(u => new CustomerUnitLineDto(
                u.Id, u.ProductId, u.Product?.Detail ?? string.Empty, u.SerialNumber))
            .ToList();

        int? diasSinComprar = ultimaCompra is null
            ? null
            : Math.Max(0, (int)(DateTime.UtcNow.Date - ultimaCompra.Value.Date).TotalDays);

        return new CustomerAccountDto(
            CustomerId:          customer.Id,
            DisplayName:         string.IsNullOrWhiteSpace(customer.BusinessName)
                                     ? customer.ContactName
                                     : customer.BusinessName,
            Address:             customer.Address,
            Phone:               customer.Phone,
            ZoneName:            customer.Zone?.Name,
            Balance:             balance,
            LastPurchaseAt:      ultimaCompra,
            DaysWithoutPurchase: diasSinComprar,
            Containers:          envases,
            Units:               unidades,
            Movements:           movements);
    }
}
