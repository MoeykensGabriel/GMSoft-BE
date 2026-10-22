using FluentValidation;
using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.Reports.RouteSheet;

/// <summary>
/// La hoja de ruta para imprimir: todos los clientes que el camión visita ese día de la
/// semana, en orden de recorrido, con sus envases y su saldo. Es el respaldo en papel
/// de la hoja del chofer.
/// </summary>
public record GetRouteSheetQuery(Guid VehicleId, DateOnly Date) : IRequest<RouteSheetDto>;

public record RouteSheetDto(
    Guid VehicleId, string VehicleName, string LicensePlate, DateOnly Date,
    IReadOnlyList<RouteSheetZoneDto> Zones);

/// <summary>Primero las zonas de la agenda del camión para ese día, en su orden; después el resto.</summary>
public record RouteSheetZoneDto(Guid ZoneId, string ZoneName, IReadOnlyList<RouteSheetCustomerDto> Customers);

public record RouteSheetCustomerDto(
    Guid CustomerId, int RouteOrder, string DisplayName, string Address,
    /// <summary>Positivo es deuda del cliente, como en su cuenta.</summary>
    decimal Balance,
    /// <summary>Envases en su poder por producto, contados por saldo o por unidad.</summary>
    IReadOnlyList<RouteSheetContainerDto> Containers);

public record RouteSheetContainerDto(Guid ProductId, string ProductDetail, int Quantity);

public class GetRouteSheetQueryValidator : AbstractValidator<GetRouteSheetQuery>
{
    public GetRouteSheetQueryValidator()
    {
        RuleFor(x => x.VehicleId).NotEmpty().WithMessage("Elegí el camión.");
        RuleFor(x => x.Date).GreaterThan(DateOnly.MinValue).LessThan(DateOnly.MaxValue);
    }
}

public class GetRouteSheetQueryHandler(IRouteSheetRepository repository, ICurrentUserService user)
    : IRequestHandler<GetRouteSheetQuery, RouteSheetDto>
{
    public async Task<RouteSheetDto> Handle(GetRouteSheetQuery request, CancellationToken cancellationToken)
    {
        if (!user.IsInRole(AppRoles.Admin)) throw new ForbiddenException("La hoja de ruta es solo para ADMIN.");
        var dia = request.Date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)request.Date.DayOfWeek;
        return await repository.GetAsync(request.VehicleId, request.Date, dia, cancellationToken)
            ?? throw new NotFoundException(nameof(Vehicle), request.VehicleId);
    }
}
