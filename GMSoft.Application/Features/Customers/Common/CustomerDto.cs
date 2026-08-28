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
    string? Notes,
    bool    IsActive)
{
    /// <summary>Razon social si la tiene, nombre de contacto si no.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(BusinessName) ? ContactName : BusinessName;
}
