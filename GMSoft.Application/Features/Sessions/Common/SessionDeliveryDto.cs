using GMSoft.Domain.Enums;

namespace GMSoft.Application.Features.Sessions.Common;

/// <summary>
/// Una visita del recorrido, vista desde la salida. Es lo que el admin necesita
/// para reconstruir el dia: a quien se visito, en que orden, que se le vendio y que
/// envases se movieron.
/// </summary>
public record SessionDeliveryDto(
    Guid         DeliveryId,
    Guid         CustomerId,
    string       CustomerName,
    string       CustomerAddress,
    DeliveryType Type,
    DateTime     DeliveredAt,
    decimal      Total,
    string?      Notes,
    IReadOnlyList<SessionDeliveryItemDto>      Items,
    IReadOnlyList<SessionDeliveryContainerDto> Containers);

public record SessionDeliveryItemDto(
    Guid    ProductId,
    string  ProductDetail,
    int     Quantity,
    decimal UnitPrice);

/// <summary>
/// Envases movidos en la visita. Positivo es lo que quedo en poder del cliente,
/// negativo lo que devolvio: es el signo del libro mayor, sin traducir.
/// </summary>
public record SessionDeliveryContainerDto(
    Guid   ProductId,
    string ProductDetail,
    int    Quantity);
