using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using MediatR;

namespace GMSoft.Application.Features.Sessions.Open;

public class OpenSessionCommandHandler : IRequestHandler<OpenSessionCommand, Guid>
{
    private readonly ISessionRepository _sessions;
    private readonly IDriverRepository _drivers;
    private readonly IVehicleRepository _vehicles;
    private readonly IZoneRepository _zones;
    private readonly IRepository<Product> _products;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public OpenSessionCommandHandler(
        ISessionRepository sessions,
        IDriverRepository drivers,
        IVehicleRepository vehicles,
        IZoneRepository zones,
        IRepository<Product> products,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _sessions    = sessions;
        _drivers     = drivers;
        _vehicles    = vehicles;
        _zones       = zones;
        _products    = products;
        _currentUser = currentUser;
        _unitOfWork  = unitOfWork;
    }

    public async Task<Guid> Handle(OpenSessionCommand request, CancellationToken cancellationToken)
    {
        var driverId = _currentUser.DriverId
            ?? throw new ForbiddenException("Solo un chofer puede abrir una sesion de reparto.");

        var driver = await _drivers.GetByIdAsync(driverId, cancellationToken)
            ?? throw new NotFoundException(nameof(Driver), driverId);

        if (!driver.IsActive)
            throw new ForbiddenException("El chofer esta desactivado.");

        // Una sesion abierta por vez. Con dos, las entregas se reparten entre las dos
        // y ninguna cierra bien.
        if (await _sessions.GetOpenByDriverAsync(driverId, cancellationToken) is not null)
            throw new ConflictException(
                "Ya tenes una sesion de reparto abierta. Cerrala antes de abrir otra.");

        if (driver.VehicleId is null)
            throw new BadRequestException(
                "No tenes un vehiculo asignado. Pedile al admin que te asigne uno.");

        var vehicle = await _vehicles.GetByIdAsync(driver.VehicleId.Value, cancellationToken)
            ?? throw new NotFoundException(nameof(Vehicle), driver.VehicleId.Value);

        if (await _sessions.HasOpenSessionForVehicleAsync(vehicle.Id, null, cancellationToken))
            throw new ConflictException(
                $"El vehiculo {vehicle.LicensePlate} ya esta en la calle con otra sesion abierta.");

        if (!await _zones.ExistsAsync(request.ZoneId, cancellationToken))
            throw new NotFoundException(nameof(Zone), request.ZoneId);

        // El odometro no vuelve para atras: un numero menor al que tiene el vehiculo
        // es un error de tipeo, y si entra ensucia el control de uso para siempre.
        if (request.KilometersAtOpen < vehicle.CurrentKilometers)
            throw new BadRequestException(
                $"El kilometraje no puede ser menor al del vehiculo ({vehicle.CurrentKilometers} km).");

        foreach (var linea in request.Load)
            if (!await _products.ExistsAsync(linea.ProductId, cancellationToken))
                throw new NotFoundException(nameof(Product), linea.ProductId);

        var ahora    = DateTime.UtcNow;
        var usuario  = _currentUser.UserId;
        var sessionId = Guid.Empty;

        // La sesion, su carga y el kilometraje del vehiculo van juntos: una sesion
        // abierta sin su carga arranca con el camion vacio y el cierre da faltante.
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var session = new DeliverySession
            {
                DriverId         = driverId,
                VehicleId        = vehicle.Id,
                ZoneId           = request.ZoneId,
                OpenedAt         = ahora,
                KilometersAtOpen = request.KilometersAtOpen,
                Status           = SessionStatus.Open
            };

            foreach (var linea in request.Load)
            {
                session.StockMovements.Add(new SessionStockMovement
                {
                    ProductId          = linea.ProductId,
                    State              = ContainerState.Full,
                    Quantity           = linea.Quantity,   // entra al camion
                    Type               = SessionStockMovementType.InitialLoad,
                    OccurredAt         = ahora,
                    RegisteredByUserId = usuario
                });
            }

            await _sessions.AddAsync(session, cancellationToken);

            vehicle.CurrentKilometers = request.KilometersAtOpen;
            _vehicles.Update(vehicle);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            sessionId = session.Id;
        }, cancellationToken);

        return sessionId;
    }
}
