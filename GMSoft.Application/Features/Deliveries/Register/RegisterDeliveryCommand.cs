using GMSoft.Domain.Enums;
using MediatR;

namespace GMSoft.Application.Features.Deliveries.Register;

/// <summary>
/// La visita al cliente. Registra la venta, el movimiento de envases en las dos
/// direcciones y el cobro, si hubo, todo en una sola operacion.
///
/// La sesion no viene por parametro: es la sesion abierta del chofer que hace el
/// request. Si viniera, se podrian imputar entregas a una salida que no es la suya.
/// </summary>
public record RegisterDeliveryCommand(
    Guid?            CustomerId,
    NewCustomerLine? NewCustomer,
    DeliveryType     Type,
    IReadOnlyList<DeliveryItemLine> Items,
    IReadOnlyList<ContainerLine>    ContainersOut,
    IReadOnlyList<ContainerLine>    ContainersIn,
    PaymentLine?     Payment,
    string?          Notes) : IRequest<RegisterDeliveryResult>;

/// <summary>Lo vendido. El precio no viaja: lo resuelve el servidor.</summary>
public record DeliveryItemLine(Guid ProductId, int Quantity);

/// <summary>Envases que quedaron en el cliente, o vacios que devolvio.</summary>
public record ContainerLine(Guid ProductId, int Quantity);

public record PaymentLine(decimal Amount, PaymentMethod Method);

/// <summary>
/// Alta de cliente en la puerta. El chofer solo puede hacerla si ademas le vende
/// algo; la zona y el lugar en el recorrido salen de la sesion.
/// </summary>
public record NewCustomerLine(
    string? BusinessName,
    string  ContactName,
    string  Phone,
    string  Address,
    string? Notes);

public record RegisterDeliveryResult(
    Guid    DeliveryId,
    Guid    CustomerId,
    decimal Total,
    decimal SaldoCuentaCliente);
