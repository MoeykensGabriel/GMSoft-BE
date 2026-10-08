using System.Security.Cryptography;
using System.Text.Json;
using FluentValidation;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Common.Validation;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.Customers.RoutePlanning;

public record RouteCustomerDto(Guid Id, string DisplayName, string Address, string Phone,
    Guid ZoneId, string? ZoneName, int RouteOrder, int[] VisitDays,
    Guid? VehicleId, string? VehicleName, string? VehicleLicensePlate);
public record RoutePlanningDto(IReadOnlyList<RouteCustomerDto> Items, string Version);
public record GetRoutePlanningQuery(Guid VehicleId, Guid ZoneId, int Day) : IRequest<RoutePlanningDto>;
public record RouteCustomerChange(Guid Id, int[]? VisitDays = null, Guid? VehicleId = null);
public record SaveRoutePlanningCommand(Guid VehicleId, Guid ZoneId, int Day, string Version,
    Guid[] CustomerIds, RouteCustomerChange[] Changes) : IRequest<RoutePlanningDto>;

public class GetRoutePlanningValidator : AbstractValidator<GetRoutePlanningQuery>
{
    public GetRoutePlanningValidator()
    {
        RuleFor(x => x.VehicleId).NotEmpty();
        RuleFor(x => x.ZoneId).NotEmpty();
        RuleFor(x => x.Day).InclusiveBetween(1, 7);
    }
}

public class RouteCustomerChangeValidator : AbstractValidator<RouteCustomerChange>
{
    public RouteCustomerChangeValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.VisitDays).ValidVisitDays().When(x => x.VisitDays is not null);
        RuleFor(x => x.VehicleId).NotEqual(Guid.Empty).When(x => x.VehicleId is not null);
    }
}

public class SaveRoutePlanningValidator : AbstractValidator<SaveRoutePlanningCommand>
{
    public SaveRoutePlanningValidator()
    {
        RuleFor(x => x.VehicleId).NotEmpty();
        RuleFor(x => x.ZoneId).NotEmpty();
        RuleFor(x => x.Day).InclusiveBetween(1, 7);
        RuleFor(x => x.Version).NotEmpty();
        RuleFor(x => x.CustomerIds).NotNull();
        RuleFor(x => x.Changes).NotNull();
        RuleForEach(x => x.Changes).NotNull().SetValidator(new RouteCustomerChangeValidator());
        RuleFor(x => x.Changes).Must(x => x is null ||
            x.Where(c => c is not null).Select(c => c.Id).Distinct().Count() == x.Length)
            .WithMessage("No repitas clientes en los cambios.");
    }
}

public class RoutePlanningHandler(IRoutePlanningRepository routes, ICustomerRepository customers,
    IVehicleRepository vehicles, IZoneRepository zones, IUnitOfWork unitOfWork)
    : IRequestHandler<GetRoutePlanningQuery, RoutePlanningDto>,
      IRequestHandler<SaveRoutePlanningCommand, RoutePlanningDto>
{
    public const string ReloadMessage = "El recorrido cambió. Recargá el recorrido antes de guardar.";

    public async Task<RoutePlanningDto> Handle(GetRoutePlanningQuery request, CancellationToken ct)
    {
        var validation = await new GetRoutePlanningValidator().ValidateAsync(request, ct);
        if (!validation.IsValid) throw new GMSoft.Application.Common.Exceptions.ValidationException(validation.Errors);
        await EnsureScope(request.VehicleId, request.ZoneId, ct);
        return Snapshot(await routes.GetRouteAsync(request.VehicleId, request.ZoneId, request.Day, ct),
            request.VehicleId, request.ZoneId, request.Day);
    }

    public async Task<RoutePlanningDto> Handle(SaveRoutePlanningCommand request, CancellationToken ct)
    {
        var validation = await new SaveRoutePlanningValidator().ValidateAsync(request, ct);
        if (!validation.IsValid) throw new GMSoft.Application.Common.Exceptions.ValidationException(validation.Errors);
        RoutePlanningDto result = null!;
        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await routes.LockCustomersAsync(ct);
            await EnsureScope(request.VehicleId, request.ZoneId, ct);
            var current = await routes.GetRouteAsync(request.VehicleId, request.ZoneId, request.Day, ct);
            var byId = current.ToDictionary(c => c.Id);
            // Missing resources are 404; existing customers outside this list are a stale list (409).
            foreach (var id in request.CustomerIds.Concat(request.Changes.Select(c => c.Id)).Distinct())
                if (!byId.ContainsKey(id) && !await customers.ExistsAsync(id, ct))
                    throw new NotFoundException(nameof(Customer), id);

            if (request.CustomerIds.Length != current.Count ||
                request.CustomerIds.Distinct().Count() != current.Count ||
                request.CustomerIds.Any(id => !byId.ContainsKey(id)) ||
                Snapshot(current, request.VehicleId, request.ZoneId, request.Day).Version != request.Version)
                throw new ConflictException(ReloadMessage);

            // Validate the entire batch before touching any tracked entity.
            foreach (var change in request.Changes)
            {
                if (!byId.ContainsKey(change.Id))
                    throw new BadRequestException("Solo podés modificar clientes del recorrido cargado.");
                if (change.VehicleId is { } vehicleId && !await vehicles.ExistsAsync(vehicleId, ct))
                    throw new NotFoundException(nameof(Vehicle), vehicleId);
            }

            var positions = current.Select(c => c.RouteOrder).Order().ToArray();
            for (var i = 0; i < request.CustomerIds.Length; i++)
                byId[request.CustomerIds[i]].RouteOrder = positions[i];
            foreach (var change in request.Changes)
            {
                var customer = byId[change.Id];
                if (change.VisitDays is not null) customer.VisitDays = change.VisitDays.Order().ToArray();
                if (change.VehicleId is { } vehicleId) customer.VehicleId = vehicleId;
            }
            await unitOfWork.SaveChangesAsync(ct);
            result = Snapshot(await routes.GetRouteAsync(request.VehicleId, request.ZoneId, request.Day, ct),
                request.VehicleId, request.ZoneId, request.Day);
        }, ct);
        return result;
    }

    private async Task EnsureScope(Guid vehicleId, Guid zoneId, CancellationToken ct)
    {
        if (!await vehicles.ExistsAsync(vehicleId, ct)) throw new NotFoundException(nameof(Vehicle), vehicleId);
        if (!await zones.ExistsAsync(zoneId, ct)) throw new NotFoundException(nameof(Zone), zoneId);
    }

    public static RoutePlanningDto Snapshot(IReadOnlyList<Customer> customers, Guid vehicleId, Guid zoneId, int day)
    {
        var ordered = customers.OrderBy(c => c.RouteOrder).ThenBy(c => c.Id).ToArray();
        var items = ordered.Select(c => new RouteCustomerDto(c.Id,
            string.IsNullOrWhiteSpace(c.BusinessName) ? c.ContactName : c.BusinessName,
            c.Address, c.Phone, c.ZoneId, c.Zone?.Name, c.RouteOrder, c.VisitDays!.Order().ToArray(),
            c.VehicleId, c.Vehicle?.Name, c.Vehicle?.LicensePlate)).ToArray();
        // PostgreSQL stores microseconds: normalize ticks so the save response matches the next read.
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { vehicleId, zoneId, day, items,
            revisions = ordered.Select(c => c.UpdatedAt.Ticks / 10).ToArray() });
        return new(items, Convert.ToHexString(SHA256.HashData(bytes)));
    }
}
