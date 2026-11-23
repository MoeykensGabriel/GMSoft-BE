using FluentValidation;
using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Models;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using MediatR;

namespace GMSoft.Application.Features.Inspections;

// Lo que la oficina ve de los controles en ruta: cada cliente controlado, cuándo, por quién,
// en qué camión, qué había marcado el chofer y qué encontró el controlador.

public record InspectionReportCountDto(string Product, int SystemQuantity, int RealQuantity);

public record InspectionReportDto(Guid Id, Guid SessionId, DateTime SessionOpenedAt, int RouteOrder, DateTime InspectedAt, string InspectorName, string VehicleName,
    string LicensePlate, string DriverName, Guid CustomerId, string CustomerName, string Address, string ZoneName,
    DriverVisitDto? DriverVisit, bool StillBuys, NotBuyingReason? NotBuyingReason, string? Notes,
    IReadOnlyList<InspectionReportCountDto> Containers);

public record GetInspectionReportQuery(Guid? InspectorId = null, Guid? VehicleId = null, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<InspectionReportDto>>;

public interface IInspectionReportRepository
{
    Task<PagedResult<InspectionReportDto>> GetAsync(GetInspectionReportQuery query, CancellationToken ct);
}

public class GetInspectionReportValidator : AbstractValidator<GetInspectionReportQuery>
{
    public GetInspectionReportValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public class GetInspectionReportHandler(IInspectionReportRepository repository, ICurrentUserService user)
    : IRequestHandler<GetInspectionReportQuery, PagedResult<InspectionReportDto>>
{
    public Task<PagedResult<InspectionReportDto>> Handle(GetInspectionReportQuery request, CancellationToken ct)
    {
        if (!user.IsInRole(AppRoles.Admin)) throw new ForbiddenException("Los controles en ruta son solo para ADMIN.");
        return repository.GetAsync(request, ct);
    }
}
