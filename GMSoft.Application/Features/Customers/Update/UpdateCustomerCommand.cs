using MediatR;

namespace GMSoft.Application.Features.Customers.Update;

/// <summary>
/// RouteOrder en nulo deja la posicion como esta. Se manda con valor solo para
/// reordenar el recorrido a mano.
/// </summary>
public record UpdateCustomerCommand(
    Guid    Id,
    string? BusinessName,
    string  ContactName,
    string  Phone,
    string  Address,
    string? Email,
    Guid    ZoneId,
    int?    RouteOrder,
    string? Notes,
    bool    IsActive) : IRequest;
