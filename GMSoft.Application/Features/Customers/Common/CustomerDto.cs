namespace GMSoft.Application.Features.Customers.Common;

public record CustomerDto(
    Guid    Id,
    string? BusinessName,
    string  ContactName,
    string  Phone,
    string  Address,
    string? Email,
    Guid    ZoneId,
    string? ZoneName,
    int     RouteOrder,
    string?   Notes,
    bool      IsActive,
    DateTime? LastPurchaseAt,
    int?      DaysWithoutPurchase)
{
    /// <summary>Razon social si la tiene, nombre de contacto si no.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(BusinessName) ? ContactName : BusinessName;

    /// <summary>
    /// Nunca registro una compra. Distinto de llevar muchos dias sin comprar: puede
    /// ser un cliente nuevo, o uno que ya compraba antes de que existiera el sistema.
    /// </summary>
    public bool SinComprasRegistradas => LastPurchaseAt is null;
}
