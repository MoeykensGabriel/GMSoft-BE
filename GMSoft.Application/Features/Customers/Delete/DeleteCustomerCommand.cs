using MediatR;

namespace GMSoft.Application.Features.Customers.Delete;

public record DeleteCustomerCommand(Guid Id) : IRequest;
