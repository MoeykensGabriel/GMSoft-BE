using MediatR;

namespace GMSoft.Application.Features.Customers.Create;

/// <summary>
/// Alta de cliente desde la oficina, sin venta asociada. El alta que hace el chofer
/// en la calle va junto con la venta y es otro caso de uso.
/// </summary>
public record CreateCustomerCommand(
    string? BusinessName,
    string  ContactName,
    string  Phone,
    string  Address,
    string? Email,
    Guid    ZoneId,
    string? Notes) : IRequest<Guid>;
