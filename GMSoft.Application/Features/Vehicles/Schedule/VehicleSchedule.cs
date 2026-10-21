using FluentValidation;
using GMSoft.Application.Common;
using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Common.Models;
using GMSoft.Application.Common.Validation;
using GMSoft.Application.Features.VehicleLoads.Common;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.Vehicles.Schedule;

public record ScheduleZoneDto(Guid ZoneId, string ZoneName);
/// <summary>
/// Un día de la agenda. <c>ZoneId</c>/<c>ZoneName</c> son la primera zona (la que
/// entendían las versiones anteriores); <c>Zones</c>, todas en orden. Sin zonas no sale.
/// </summary>
public record ScheduleDayDto(int Day, Guid? ZoneId, string? ZoneName)
{
    public IReadOnlyList<ScheduleZoneDto> Zones { get; init; } =
        ZoneId is Guid id ? [new ScheduleZoneDto(id, ZoneName ?? string.Empty)] : [];
}
public record VehicleScheduleDto(Guid VehicleId, bool IsConfigured, bool IsSuggested, IReadOnlyList<ScheduleDayDto> Days);
/// <summary><c>ZoneIds</c> en orden; sin ellas vale <c>ZoneId</c> sola (o ninguna: no sale).</summary>
public record ScheduleDayInput(int Day, Guid? ZoneId, Guid[]? ZoneIds = null)
{
    public const int MaxZones = 5;
    public Guid[] EffectiveZoneIds => ZoneIds ?? (ZoneId is Guid id ? [id] : []);
}
public record GetVehicleScheduleQuery(Guid VehicleId) : IRequest<VehicleScheduleDto>;
public record SaveVehicleScheduleCommand(Guid VehicleId, ScheduleDayInput[] Days) : IRequest<VehicleScheduleDto>;
public record CoverageDayDto(int Day, Guid? ScheduledZoneId, string? ScheduledZoneName, bool? Matches);
public record CoverageDto(bool? Covered, IReadOnlyList<CoverageDayDto> Days);
public record CheckCoverageQuery(Guid VehicleId, Guid ZoneId, int[] VisitDays) : IRequest<CoverageDto>;
public record SuggestedZoneQuery(Guid? DriverId = null) : IRequest<SuggestedZoneDto>;
public record SuggestedZoneDto(Guid VehicleId, Guid? ZoneId, string? ZoneName, bool IsConfigured,
    bool IsSuggested, int[] RouteDays, IReadOnlyList<ScheduleDayDto> Days)
{
    /// <summary>Las zonas sugeridas para abrir, en orden; la primera es <c>ZoneId</c>.</summary>
    public IReadOnlyList<ScheduleZoneDto> Zones { get; init; } = [];
}
public record UncoveredCustomerDto(Guid Id, string DisplayName, string Address, Guid VehicleId,
    Guid ZoneId, string? ZoneName, int[] VisitDays, int[] UncoveredDays);
public record GetUncoveredQuery(Guid? VehicleId = null, int Page = 1, int PageSize = 50) : IRequest<PagedResult<UncoveredCustomerDto>>;
public record ScheduleHistory(Guid ZoneId, string ZoneName, DateTime OpenedAt, int[]? RouteDays);

public interface IVehicleScheduleRepository
{
    Task<IReadOnlyList<ScheduleDayDto>> GetAsync(Guid vehicleId, CancellationToken ct);
    Task ReplaceAsync(Guid vehicleId, ScheduleDayInput[] days, CancellationToken ct);
    Task<IReadOnlyList<ScheduleHistory>> HistoryAsync(Guid vehicleId, DateTime from, DateTime to, CancellationToken ct);
    Task<PagedResult<UncoveredCustomerDto>> UncoveredAsync(GetUncoveredQuery query, CancellationToken ct);
}

public class SaveVehicleScheduleValidator : AbstractValidator<SaveVehicleScheduleCommand>
{
    public SaveVehicleScheduleValidator()
    {
        RuleFor(x => x.VehicleId).NotEmpty();
        RuleFor(x => x.Days).NotNull().Must(days => days is { Length: 7 } &&
            days.All(d => d is not null && d.Day >= 1 && d.Day <= 7 && d.ZoneId != Guid.Empty) &&
            days.Select(d => d.Day).Distinct().Count() == 7)
            .WithMessage("Envia los siete dias ISO, sin repetir; zoneId null significa que no sale.")
            .Must(days => days is null || days.All(d => d is null || (
                d.EffectiveZoneIds.All(id => id != Guid.Empty) &&
                d.EffectiveZoneIds.Distinct().Count() == d.EffectiveZoneIds.Length &&
                d.EffectiveZoneIds.Length <= ScheduleDayInput.MaxZones)))
            .WithMessage($"Cada dia lleva hasta {ScheduleDayInput.MaxZones} zonas, sin repetir.");
    }
}
public class CheckCoverageValidator : AbstractValidator<CheckCoverageQuery>
{
    public CheckCoverageValidator()
    {
        RuleFor(x => x.VehicleId).NotEmpty();
        RuleFor(x => x.ZoneId).NotEmpty();
        RuleFor(x => x.VisitDays).ValidVisitDays();
    }
}

public static class ScheduleRules
{
    public static IReadOnlyList<ScheduleDayDto> Suggest(IEnumerable<ScheduleHistory> history) =>
        Enumerable.Range(1, 7).Select(day =>
        {
            var winner = history.Where(h => (h.RouteDays is { Length: > 0 } ? h.RouteDays : [BusinessTime.IsoDayOfWeek(h.OpenedAt)]).Contains(day))
                .GroupBy(h => new { h.ZoneId, h.ZoneName }).OrderByDescending(g => g.Count())
                .ThenByDescending(g => g.Max(h => h.OpenedAt)).ThenBy(g => g.Key.ZoneId).FirstOrDefault();
            return new ScheduleDayDto(day, winner?.Key.ZoneId, winner?.Key.ZoneName);
        }).ToArray();

    public static CoverageDto Coverage(IReadOnlyList<ScheduleDayDto> schedule, Guid zoneId, int[] days)
    {
        var configured = schedule.Count > 0;
        var result = days.Order().Select(day =>
        {
            var entry = schedule.FirstOrDefault(x => x.Day == day);
            // Cubre si la zona es cualquiera de las del día, no solo la primera.
            return new CoverageDayDto(day, entry?.ZoneId, entry?.ZoneName,
                configured ? entry is not null && entry.Zones.Any(z => z.ZoneId == zoneId) : null);
        }).ToArray();
        return new(configured ? result.All(x => x.Matches == true) : null, result);
    }
}

public class VehicleScheduleHandler(IVehicleScheduleRepository schedules, IVehicleRepository vehicles,
    IDriverRepository drivers, IZoneRepository zones, IVehicleLoadRepository loads,
    ICurrentUserService user, IUnitOfWork uow, TimeProvider clock) :
    IRequestHandler<GetVehicleScheduleQuery, VehicleScheduleDto>,
    IRequestHandler<SaveVehicleScheduleCommand, VehicleScheduleDto>,
    IRequestHandler<CheckCoverageQuery, CoverageDto>,
    IRequestHandler<SuggestedZoneQuery, SuggestedZoneDto>,
    IRequestHandler<GetUncoveredQuery, PagedResult<UncoveredCustomerDto>>
{
    private void Admin() { if (!user.IsInRole(AppRoles.Admin)) throw new ForbiddenException("Solo ADMIN puede realizar esta operacion."); }
    private async Task EnsureVehicle(Guid id, CancellationToken ct)
    { if (!await vehicles.ExistsAsync(id, ct)) throw new NotFoundException(nameof(Vehicle), id); }
    private async Task<Driver> Driver(Guid id, CancellationToken ct)
    {
        var driver = await drivers.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Driver), id);
        if (!driver.IsActive) throw new ForbiddenException("El chofer esta desactivado.");
        return driver;
    }
    private async Task<VehicleScheduleDto> Read(Guid id, CancellationToken ct)
    {
        var saved = await schedules.GetAsync(id, ct);
        if (saved.Count > 0) return new(id, true, false, saved);
        var now = clock.GetUtcNow().UtcDateTime;
        return new(id, false, true, ScheduleRules.Suggest(await schedules.HistoryAsync(id, now.AddDays(-56), now, ct)));
    }
    public async Task<VehicleScheduleDto> Handle(GetVehicleScheduleQuery r, CancellationToken ct)
    {
        if (!user.IsInRole(AppRoles.Admin))
        {
            var driver = await Driver(user.DriverId ?? throw new ForbiddenException("Se requiere un chofer autenticado."), ct);
            if (driver.VehicleId != r.VehicleId) throw new ForbiddenException("Solo podes consultar tu camion.");
        }
        await EnsureVehicle(r.VehicleId, ct);
        return await Read(r.VehicleId, ct);
    }
    public async Task<VehicleScheduleDto> Handle(SaveVehicleScheduleCommand r, CancellationToken ct)
    {
        Admin();
        var validation = new SaveVehicleScheduleValidator().Validate(r);
        if (!validation.IsValid) throw new GMSoft.Application.Common.Exceptions.ValidationException(validation.Errors);
        await uow.ExecuteInTransactionAsync(async () =>
        {
            if (await vehicles.LockAsync(r.VehicleId, ct) is null) throw new NotFoundException(nameof(Vehicle), r.VehicleId);
            foreach (var id in r.Days.SelectMany(d => d.EffectiveZoneIds).Distinct())
            {
                var zone = await zones.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Zone), id);
                if (!zone.IsActive) throw new BadRequestException("La zona debe estar activa.");
            }
            await schedules.ReplaceAsync(r.VehicleId, r.Days, ct);
            await uow.SaveChangesAsync(ct);
        }, ct);
        return await Read(r.VehicleId, ct);
    }
    public async Task<CoverageDto> Handle(CheckCoverageQuery r, CancellationToken ct)
    {
        Admin();
        var validation = new CheckCoverageValidator().Validate(r);
        if (!validation.IsValid) throw new GMSoft.Application.Common.Exceptions.ValidationException(validation.Errors);
        await EnsureVehicle(r.VehicleId, ct);
        return ScheduleRules.Coverage(await schedules.GetAsync(r.VehicleId, ct), r.ZoneId, r.VisitDays);
    }
    public async Task<SuggestedZoneDto> Handle(SuggestedZoneQuery r, CancellationToken ct)
    {
        if (r.DriverId.HasValue && !user.IsInRole(AppRoles.Admin)) throw new ForbiddenException("Solo ADMIN puede elegir chofer.");
        var id = user.IsInRole(AppRoles.Admin) ? r.DriverId ?? throw new BadRequestException("driverId es obligatorio para ADMIN.")
            : user.DriverId ?? throw new ForbiddenException("Se requiere un chofer autenticado.");
        var driver = await Driver(id, ct);
        var vehicleId = driver.VehicleId ?? throw new BadRequestException("El chofer no tiene camion asignado.");
        await EnsureVehicle(vehicleId, ct);
        var pending = await loads.GetPendingAsync(vehicleId, ct);
        var days = pending.Count == 0 ? [] : RouteDaySelection.Resolve(pending, clock.GetUtcNow().UtcDateTime);
        var schedule = await Read(vehicleId, ct);
        var selected = schedule.Days.Where(d => days.Contains(d.Day)).OrderBy(d => d.Day).ToArray();
        // Las zonas de los días del recorrido, en orden y sin repetir: la carga suele ser
        // de un día, pero si cubre dos se recorren las de ambos.
        var zonas = selected.SelectMany(d => d.Zones).DistinctBy(z => z.ZoneId).ToList();
        return new(vehicleId, zonas.FirstOrDefault()?.ZoneId, zonas.FirstOrDefault()?.ZoneName,
            schedule.IsConfigured, schedule.IsSuggested, days, selected) { Zones = zonas };
    }
    public async Task<PagedResult<UncoveredCustomerDto>> Handle(GetUncoveredQuery r, CancellationToken ct)
    {
        Admin();
        if (r.Page < 1 || r.PageSize < 1 || r.PageSize > 200 || (long)(r.Page - 1) * r.PageSize > int.MaxValue)
            throw new BadRequestException("Paginacion invalida: page >= 1; pageSize entre 1 y 200.");
        if (r.VehicleId.HasValue) await EnsureVehicle(r.VehicleId.Value, ct);
        return await schedules.UncoveredAsync(r, ct);
    }
}

