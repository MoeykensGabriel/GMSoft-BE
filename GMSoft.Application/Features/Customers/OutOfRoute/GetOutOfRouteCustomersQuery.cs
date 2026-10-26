using FluentValidation;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Customers.Common;
using MediatR;

namespace GMSoft.Application.Features.Customers.OutOfRoute;

public record OutOfRouteCustomerDto(Guid Id, string Name, string Address, Guid ZoneId, string ZoneName, int[] VisitDays);
public record GetOutOfRouteCustomersQuery(string Search = "") : IRequest<IReadOnlyList<OutOfRouteCustomerDto>>;

public class GetOutOfRouteCustomersQueryValidator : AbstractValidator<GetOutOfRouteCustomersQuery>
{
    public GetOutOfRouteCustomersQueryValidator()
    {
        RuleFor(x => x.Search).Must(s => s is not null && s.Trim().Length >= 2)
            .WithMessage("Ingresá al menos 2 caracteres para buscar.");
    }
}

public class GetOutOfRouteCustomersQueryHandler(ICustomerRepository customers, CustomerRouteAccess access)
    : IRequestHandler<GetOutOfRouteCustomersQuery, IReadOnlyList<OutOfRouteCustomerDto>>
{
    public async Task<IReadOnlyList<OutOfRouteCustomerDto>> Handle(GetOutOfRouteCustomersQuery request, CancellationToken ct)
    {
        var session = await access.GetDriverSessionAsync(ct)
            ?? throw new ForbiddenException("La búsqueda es solo para choferes con salida abierta.");
        var rows = await customers.SearchOutOfRouteAsync(session.VehicleId, CustomerRouteAccess.ZoneIds(session),
            CustomerRouteAccess.Days(session), request.Search.Trim(), ct);
        return rows.Select(c => new OutOfRouteCustomerDto(c.Id,
            string.IsNullOrWhiteSpace(c.BusinessName) ? c.ContactName : c.BusinessName,
            c.Address, c.ZoneId, c.Zone.Name, c.VisitDays ?? [])).ToList();
    }
}
