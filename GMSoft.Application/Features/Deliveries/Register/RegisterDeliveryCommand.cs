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
    // En ventas y promociones se deriva de Items para productos ByBalance. Puede enviarse vacio.
    IReadOnlyList<ContainerLine>    ContainersOut,
    IReadOnlyList<ContainerLine>    ContainersIn,
    PaymentLine?     Payment,
    string?          Notes,
    // Lo genera el telefono por visita. Repetirlo devuelve la visita ya registrada.
    Guid?            ClientRequestId = null) : IRequest<RegisterDeliveryResult>;

/// <summary>Lo vendido. El precio no viaja: lo resuelve el servidor.</summary>
public record DeliveryItemLine(Guid ProductId, int Quantity);

/// <summary>Envases que quedaron en el cliente, o vacios que devolvio.</summary>
public record ContainerLine(Guid ProductId, int Quantity);

/// <summary>
/// El cobro de la visita. Sin importe significa "cobro la venta completa": el total
/// lo pone el servidor con los precios que resolvio, no el telefono. Un importe
/// explicito queda para cobros que no coinciden con la venta.
/// </summary>
public record PaymentLine(decimal? Amount, PaymentMethod Method);

/// <summary>
/// Alta de cliente en la puerta junto con una venta o promocion;
/// la zona y el lugar en el recorrido salen de la sesion.
/// </summary>
public record NewCustomerLine(
    string? BusinessName,
    string  ContactName,
    string  Phone,
    string  Address,
    string? Notes, int[]? VisitDays = null);

public record RegisterDeliveryResult(
    Guid    DeliveryId,
    Guid    CustomerId,
    decimal Total,
    decimal SaldoCuentaCliente);
