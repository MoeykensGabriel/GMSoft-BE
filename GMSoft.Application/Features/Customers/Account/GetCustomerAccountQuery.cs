using MediatR;

namespace GMSoft.Application.Features.Customers.Account;

public record GetCustomerAccountQuery(Guid Id, int MovementsLimit = 50, int StatementLimit = 60)
    : IRequest<CustomerAccountDto>;
