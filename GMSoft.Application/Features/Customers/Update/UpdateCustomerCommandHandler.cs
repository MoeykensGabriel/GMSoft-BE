using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.Customers.Update;

public class UpdateCustomerCommandHandler : IRequestHandler<UpdateCustomerCommand>
{
    private readonly ICustomerRepository _customers;
    private readonly IZoneRepository _zones;
    private readonly IVehicleRepository _vehicles;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCustomerCommandHandler(
        ICustomerRepository customers,
        IZoneRepository zones,
        IVehicleRepository vehicles,
        IUnitOfWork unitOfWork)
    {
        _customers  = customers;
        _zones      = zones;
        _vehicles   = vehicles;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await _customers.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.Id);

        if (request.VehicleId is null || request.VehicleId == Guid.Empty)
            throw new BadRequestException("Seleccioná un camión para el cliente.");
        if (!await _vehicles.ExistsAsync(request.VehicleId.Value, cancellationToken))
            throw new NotFoundException(nameof(Vehicle), request.VehicleId.Value);

        if (!await _zones.ExistsAsync(request.ZoneId, cancellationToken))
            throw new NotFoundException(nameof(Zone), request.ZoneId);

        var cambiaDeRecorrido = customer.ZoneId != request.ZoneId || customer.VehicleId != request.VehicleId;

        customer.BusinessName = string.IsNullOrWhiteSpace(request.BusinessName)
            ? null
            : request.BusinessName.Trim();
        customer.ContactName = request.ContactName.Trim();
        customer.Phone       = request.Phone.Trim();
        customer.Address     = request.Address.Trim();
        customer.Email       = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        customer.ZoneId      = request.ZoneId;
        customer.VehicleId   = request.VehicleId;
        customer.Notes       = request.Notes?.Trim();
        customer.IsActive    = request.IsActive;
        customer.VisitDays   = request.VisitDays!.Order().ToArray();

        if (cambiaDeRecorrido)
        {
            // Cambiar camión o zona lo coloca al final del recorrido de destino.
            customer.RouteOrder = await _customers.GetNextRouteOrderAsync(
                request.ZoneId, cancellationToken);
        }

        else if (request.RouteOrder is not null)
        {
            customer.RouteOrder = request.RouteOrder.Value;
        }

        _customers.Update(customer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
