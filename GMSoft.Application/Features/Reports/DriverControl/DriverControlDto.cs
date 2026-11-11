using GMSoft.Domain.Enums;

namespace GMSoft.Application.Features.Reports.DriverControl;

/// <summary>
/// Control de choferes: a qué hora abrió cada salida y cómo marcó las visitas. Sirve para
/// preguntarle al chofer con datos: «el martes abriste a las 11:40» o «marcaste 13 clientes
/// en 79 segundos».
/// </summary>
public record DriverControlDto(DateOnly From, DateOnly To, IReadOnlyList<DriverControlDriverDto> Drivers)
{
    /// <summary>Visitas seguidas a menos de esta distancia forman una ráfaga.</summary>
    public int BurstGapSeconds { get; init; } = (int)DriverControlAnalysis.BurstGap.TotalSeconds;
    /// <summary>Visitas seguidas necesarias para contar como ráfaga.</summary>
    public int BurstMinVisits { get; init; } = DriverControlAnalysis.BurstMinVisits;
    /// <summary>Minutos de demora desde los que una visita cuenta como enviada sin señal.</summary>
    public int OfflineDelayMinutes { get; init; } =
        (int)Deliveries.Register.DeliveryMark.OfflineDelay.TotalMinutes;
}

public record DriverControlDriverDto(
    Guid DriverId,
    string DriverName,
    int SessionCount,
    int Visits,
    int Bursts,
    int BurstVisits,
    int OfflineVisits,
    int DoubtfulVisits,
    int OutsideHoursVisits,
    int OutsideHoursOpenings,
    int NoPurchase,
    int DidNotWant,
    int? MedianIntervalSeconds,
    /// <summary>Hora de apertura habitual (mediana), en hora de Argentina.</summary>
    TimeOnly? UsualOpening,
    IReadOnlyList<DriverControlSessionDto> Sessions);

public record DriverControlSessionDto(
    Guid SessionId,
    string VehicleName,
    string LicensePlate,
    string ZoneName,
    SessionStatus Status,
    DateTime OpenedAt,
    bool OpenedOutsideWorkingHours,
    /// <summary>Primera carga de la oficina que se llevó esta salida.</summary>
    DateTime? LoadedAt,
    DateTime? ClosedAt,
    DateTime? FirstVisitAt,
    DateTime? LastVisitAt,
    int Visits,
    int? MedianIntervalSeconds,
    int OfflineVisits,
    int DoubtfulVisits,
    int OutsideHoursVisits,
    int NoPurchase,
    int DidNotWant,
    IReadOnlyList<DriverControlBurstDto> Bursts,
    IReadOnlyList<DriverControlMarkDto> Marks);

/// <summary>
/// Visitas marcadas casi juntas. Con la hora del celular es una carga en bloque; con la
/// del servidor (visitas de apps viejas) puede ser un reenvío por falta de señal.
/// </summary>
public record DriverControlBurstDto(DateTime Start, DateTime End, int Count, bool ServerTime);

/// <param name="At">Hora del celular si es confiable; si no, la de llegada al servidor.</param>
public record DriverControlMarkDto(
    DateTime At,
    DateTime DeliveredAt,
    bool PhoneTime,
    string CustomerName,
    DeliveryType Type,
    NoPurchaseReason? NoPurchaseReason,
    bool Offline,
    bool Doubtful,
    bool InBurst,
    bool OutsideWorkingHours);

/// <summary>Lo que trae la base: una fila por salida.</summary>
public record DriverControlSessionRow(
    Guid Id, Guid DriverId, string DriverName, string VehicleName, string LicensePlate, string ZoneName,
    DateTime OpenedAt, DateTime? ClosedAt, SessionStatus Status, bool OpenedOutsideWorkingHours, DateTime? LoadedAt);

/// <summary>Lo que trae la base: una fila por visita.</summary>
public record DriverControlVisitRow(
    Guid SessionId, string CustomerName, DeliveryType Type, NoPurchaseReason? NoPurchaseReason,
    DateTime DeliveredAt, DateTime? MarkedAt, bool MarkedAtDoubtful, bool IsOutsideWorkingHours);
