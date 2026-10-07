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
    public Guid? VehicleId { get; init; }
    public string? VehicleName { get; init; }
    public string? VehicleLicensePlate { get; init; }
    public DateTime? LastVisitAt { get; init; }
    public int[]? VisitDays { get; init; }
    /// <summary>
    /// Semanas en que el camion salio a visitarlo y volvio sin venderle, desde su
    /// ultima compra. No son semanas de calendario: sin reparto no suma.
    /// </summary>
    public int WeeksWithoutPurchase { get; init; }

    /// <summary>White, Red o Black segun los turnos perdidos.</summary>
    public string ActivityStatus { get; init; } = "White";

    /// <summary>Razon social si la tiene, nombre de contacto si no.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(BusinessName) ? ContactName : BusinessName;

    /// <summary>
    /// Nunca registro una compra. Distinto de llevar muchos dias sin comprar: puede
    /// ser un cliente nuevo, o uno que ya compraba antes de que existiera el sistema.
    /// </summary>
    public bool SinComprasRegistradas => LastPurchaseAt is null;
}
