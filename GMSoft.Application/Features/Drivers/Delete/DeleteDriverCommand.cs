using MediatR;

namespace GMSoft.Application.Features.Drivers.Delete;

public record DeleteDriverCommand(Guid Id) : IRequest;
