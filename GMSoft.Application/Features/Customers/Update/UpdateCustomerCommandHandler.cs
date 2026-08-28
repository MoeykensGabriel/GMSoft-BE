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
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCustomerCommandHandler(
        ICustomerRepository customers,
        IZoneRepository zones,
        IUnitOfWork unitOfWork)
    {
        _customers  = customers;
        _zones      = zones;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await _customers.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.Id);

        if (!await _zones.ExistsAsync(request.ZoneId, cancellationToken))
            throw new NotFoundException(nameof(Zone), request.ZoneId);

        var cambiaDeZona = customer.ZoneId != request.ZoneId;

        customer.BusinessName = string.IsNullOrWhiteSpace(request.BusinessName)
            ? null
            : request.BusinessName.Trim();
        customer.ContactName = request.ContactName.Trim();
        customer.Phone       = request.Phone.Trim();
        customer.Address     = request.Address.Trim();
        customer.Email       = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        customer.ZoneId      = request.ZoneId;
        customer.Notes       = request.Notes?.Trim();
        customer.IsActive    = request.IsActive;

        if (request.RouteOrder is not null)
        {
            customer.RouteOrder = request.RouteOrder.Value;
        }
        else if (cambiaDeZona)
        {
            // Cambio de zona sin posicion indicada: su lugar viejo no significa nada
            // en el recorrido nuevo, asi que va al final del nuevo.
            customer.RouteOrder = await _customers.GetNextRouteOrderAsync(
                request.ZoneId, cancellationToken);
        }

        _customers.Update(customer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
