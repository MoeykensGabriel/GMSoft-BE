using GMSoft.Application.Features.Customers.Common;
using MediatR;

namespace GMSoft.Application.Features.Customers.GetById;

public record GetCustomerByIdQuery(Guid Id) : IRequest<CustomerDto>;
