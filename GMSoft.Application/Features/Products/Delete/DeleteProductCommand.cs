using MediatR;

namespace GMSoft.Application.Features.Products.Delete;

public record DeleteProductCommand(Guid Id) : IRequest;
