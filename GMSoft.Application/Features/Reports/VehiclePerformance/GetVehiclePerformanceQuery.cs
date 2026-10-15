using FluentValidation;
using GMSoft.Application.Common;
using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using MediatR;

namespace GMSoft.Application.Features.Reports.VehiclePerformance;

public record GetVehiclePerformanceQuery(DateOnly From, DateOnly To) : IRequest<VehiclePerformanceDto>;

public class GetVehiclePerformanceQueryValidator : AbstractValidator<GetVehiclePerformanceQuery>
{
    public GetVehiclePerformanceQueryValidator()
    {
        RuleFor(x => x.From).GreaterThan(DateOnly.MinValue).LessThan(DateOnly.MaxValue);
        RuleFor(x => x.To).GreaterThan(DateOnly.MinValue).LessThan(DateOnly.MaxValue);
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From);
        RuleFor(x => x).Must(x => x.To.DayNumber - x.From.DayNumber < 366)
            .WithMessage("El rango inclusivo no puede superar 366 dias.");
    }
}

public class GetVehiclePerformanceQueryHandler(IVehiclePerformanceRepository repository, ICurrentUserService user)
    : IRequestHandler<GetVehiclePerformanceQuery, VehiclePerformanceDto>
{
    public async Task<VehiclePerformanceDto> Handle(GetVehiclePerformanceQuery request, CancellationToken cancellationToken)
    {
        if (!user.IsInRole(AppRoles.Admin)) throw new ForbiddenException("El reporte es solo para ADMIN.");
        var from = BusinessTime.DayRangeUtc(request.From).FromUtc;
        var to = BusinessTime.DayRangeUtc(request.To).ToUtc;
        return new(request.From, request.To, await repository.GetAsync(from, to, cancellationToken));
    }
}
