using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.Customers.Create;

public class CreateCustomerCommandHandler : IRequestHandler<CreateCustomerCommand, Guid>
{
    private readonly ICustomerRepository _customers;
    private readonly IZoneRepository _zones;
    private readonly IVehicleRepository _vehicles;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCustomerCommandHandler(
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

    public async Task<Guid> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        if (request.VehicleId is null || request.VehicleId == Guid.Empty)
            throw new BadRequestException("Seleccioná un camión para el cliente.");
        if (!await _vehicles.ExistsAsync(request.VehicleId.Value, cancellationToken))
            throw new NotFoundException(nameof(Vehicle), request.VehicleId.Value);

        if (!await _zones.ExistsAsync(request.ZoneId, cancellationToken))
            throw new NotFoundException(nameof(Zone), request.ZoneId);

        var customer = new Customer
        {
            BusinessName = string.IsNullOrWhiteSpace(request.BusinessName)
                ? null
                : request.BusinessName.Trim(),
            ContactName = request.ContactName.Trim(),
            Phone       = request.Phone.Trim(),
            Address     = request.Address.Trim(),
            Email       = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            ZoneId      = request.ZoneId,
            VehicleId   = request.VehicleId,
            Notes       = request.Notes?.Trim(),
            IsActive    = true,
            VisitDays   = request.VisitDays!.Order().ToArray(),

            // Al final del recorrido de su zona: el orden del reparto es el orden
            // en que se fueron cargando los clientes.
            RouteOrder = await _customers.GetNextRouteOrderAsync(request.ZoneId, cancellationToken)
        };

        await _customers.AddAsync(customer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return customer.Id;
    }
}
