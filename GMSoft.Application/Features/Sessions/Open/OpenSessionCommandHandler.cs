using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using GMSoft.Application.Features.VehicleLoads.Common;
using MediatR;
using GMSoft.Application.Features.WorkingHours;

namespace GMSoft.Application.Features.Sessions.Open;

public class OpenSessionCommandHandler : IRequestHandler<OpenSessionCommand, Guid>
{
    private readonly ISessionRepository _sessions;
    private readonly IDriverRepository _drivers;
    private readonly IVehicleRepository _vehicles;
    private readonly IZoneRepository _zones;
    private readonly IVehicleLoadRepository _loads;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly WorkingHoursService _workingHours;

    public OpenSessionCommandHandler(
        ISessionRepository sessions,
        IDriverRepository drivers,
        IVehicleRepository vehicles,
        IZoneRepository zones,
        IVehicleLoadRepository loads,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        WorkingHoursService workingHours)
    {
        _sessions    = sessions;
        _drivers     = drivers;
        _vehicles    = vehicles;
        _zones       = zones;
        _loads       = loads;
        _currentUser = currentUser;
        _unitOfWork  = unitOfWork;
        _workingHours = workingHours;
    }

    public async Task<Guid> Handle(OpenSessionCommand request, CancellationToken cancellationToken)
    {
        if (request.DriverId.HasValue && !_currentUser.IsInRole(AppRoles.Admin))
            throw new ForbiddenException("Solo ADMIN puede elegir el chofer de una salida.");
        var driverId = (request.DriverId ?? _currentUser.DriverId)
            ?? throw new ForbiddenException("Solo un chofer puede abrir una sesion de reparto.");

        var sessionId = Guid.Empty;
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var driver = await _drivers.LockAsync(driverId, cancellationToken)
                ?? throw new NotFoundException(nameof(Driver), driverId);

            if (!driver.IsActive)
                throw new ForbiddenException("El chofer esta desactivado.");

            if (driver.VehicleId is null)
                throw new BadRequestException(
                    "No tenes un vehiculo asignado. Pedile al admin que te asigne uno.");

            var vehicle = await _vehicles.LockAsync(driver.VehicleId.Value, cancellationToken)
                ?? throw new NotFoundException(nameof(Vehicle), driver.VehicleId.Value);

            // Una sesion abierta por vez. Con dos, las entregas se reparten entre las dos
            // y ninguna cierra bien.
            if (await _sessions.GetOpenByDriverAsync(driverId, cancellationToken) is not null)
                throw new ConflictException(
                    "Ya tenés una salida abierta. ADMIN debe hacer la recepción del camión antes de iniciar otra.");

            if (await _sessions.HasOpenSessionForVehicleAsync(vehicle.Id, null, cancellationToken))
                throw new ConflictException(
                    $"El vehiculo {vehicle.LicensePlate} ya esta en la calle con otra sesion abierta.");

            var zonas = request.EffectiveZoneIds;
            foreach (var zoneId in zonas)
                if (!await _zones.ExistsAsync(zoneId, cancellationToken))
                    throw new NotFoundException(nameof(Zone), zoneId);

            // El odometro no vuelve para atras: un numero menor al que tiene el vehiculo
            // es un error de tipeo, y si entra ensucia el control de uso para siempre.
            if (request.KilometersAtOpen < vehicle.CurrentKilometers)
                throw new BadRequestException(
                    $"El kilometraje no puede ser menor al del vehiculo ({vehicle.CurrentKilometers} km).");

            // La carga previa del administrador habilita la salida del camion.
            var pendientes = await _loads.GetPendingAsync(vehicle.Id, cancellationToken);
            if (pendientes.Count == 0)
                throw new ConflictException(
                    "El administrador debe cargar los productos llenos del camion antes de iniciar el recorrido.");

            var horario = await _workingHours.EnsureAllowedAsync(true, request.ConfirmOutsideWorkingHours, _currentUser, cancellationToken);
            var ahora     = horario.UtcNow;
            var usuario   = _currentUser.UserId;

            var session = new DeliverySession
            {
                DriverId         = driverId,
                VehicleId        = vehicle.Id,
                ZoneId           = zonas[0],
                Zones            = zonas.Select((zoneId, i) => new DeliverySessionZone { ZoneId = zoneId, Position = i }).ToList(),
                OpenedAt         = ahora,
                OpenedOutsideWorkingHours = horario.IsOutsideWorkingHours,
                OpenedByUserId = usuario,
                RouteDays        = RouteDaySelection.Resolve(pendientes, ahora),
                KilometersAtOpen = request.KilometersAtOpen,
                Status           = SessionStatus.Open
            };

            foreach (var carga in pendientes)
            {
                // Un movimiento por linea de carga, sin sumar por producto: si la
                // oficina cargo en dos tandas, el libro mayor lo cuenta asi.
                session.StockMovements.Add(new SessionStockMovement
                {
                    ProductId          = carga.ProductId,
                    State              = ContainerState.Full,
                    Quantity           = carga.Quantity,   // entra al camion
                    Type               = SessionStockMovementType.InitialLoad,
                    OccurredAt         = ahora,
                    RegisteredByUserId = carga.RegisteredByUserId ?? usuario
                });

                // Por navegacion y no por id: la sesion todavia no tiene Id asignado,
                // se lo pone SaveChangesAsync y EF completa la FK sola.
                carga.ConsumedBySession = session;
            }

            await _sessions.AddAsync(session, cancellationToken);

            vehicle.CurrentKilometers = request.KilometersAtOpen;

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            sessionId = session.Id;
        }, cancellationToken);

        return sessionId;
    }
}
