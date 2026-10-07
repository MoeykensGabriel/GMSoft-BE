using GMSoft.Domain.Enums;

namespace GMSoft.Application.Features.Sessions.DetailedSettlement;

/// <summary>
/// Un cliente dentro de la liquidacion detallada de una salida: que se le vendio,
/// que envases devolvio, con que pago y cuanto quedo debiendo.
/// </summary>
/// <param name="Balance">
/// Saldo de la cuenta del cliente al cierre de la salida (o al momento de la
/// consulta si sigue abierta). Incluye deuda anterior: no es solo lo de esta visita.
/// </param>
public record CustomerSettlementDto(
    Guid    CustomerId,
    string  CustomerName,
    string  CustomerAddress,
    string  CustomerPhone,
    IReadOnlyList<CustomerSettlementLineDto>    Lines,
    IReadOnlyList<CustomerReturnedContainerDto> ReturnedContainers,
    decimal Cash,
    decimal Transfer,
    decimal Card,
    decimal Balance);

/// <summary>Una linea vendida o entregada en promocion. El importe es cantidad por precio del momento.</summary>
public record CustomerSettlementLineDto(
    DateTime     Date,
    DeliveryType Type,
    int          Quantity,
    string       ProductDetail,
    decimal      Amount);

/// <summary>Envases vacios que el cliente devolvio en la salida, en positivo.</summary>
public record CustomerReturnedContainerDto(
    string ProductDetail,
    int    Quantity);

/// <summary>Lo cobrado a un cliente en la salida con un medio de pago.</summary>
public record SessionCustomerPaymentDto(
    Guid          CustomerId,
    PaymentMethod Method,
    decimal       Amount);
