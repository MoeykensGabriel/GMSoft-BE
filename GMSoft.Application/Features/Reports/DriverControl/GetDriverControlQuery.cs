using FluentValidation;
using GMSoft.Application.Common;
using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using MediatR;

namespace GMSoft.Application.Features.Reports.DriverControl;

public record GetDriverControlQuery(DateOnly From, DateOnly To, Guid? DriverId = null) : IRequest<DriverControlDto>;

public class GetDriverControlQueryValidator : AbstractValidator<GetDriverControlQuery>
{
    public GetDriverControlQueryValidator()
    {
        RuleFor(x => x.From).GreaterThan(DateOnly.MinValue).LessThan(DateOnly.MaxValue);
        RuleFor(x => x.To).GreaterThan(DateOnly.MinValue).LessThan(DateOnly.MaxValue);
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From);
        // Trae cada marca para dibujar el día: un rango largo sería una respuesta enorme.
        RuleFor(x => x).Must(x => x.To.DayNumber - x.From.DayNumber < 62)
            .WithMessage("El rango no puede superar 62 días.");
    }
}

public class GetDriverControlQueryHandler(IDriverControlRepository repository, ICurrentUserService user)
    : IRequestHandler<GetDriverControlQuery, DriverControlDto>
{
    public async Task<DriverControlDto> Handle(GetDriverControlQuery request, CancellationToken cancellationToken)
    {
        if (!user.IsInRole(AppRoles.Admin)) throw new ForbiddenException("El control de choferes es solo para ADMIN.");
        var from = BusinessTime.DayRangeUtc(request.From).FromUtc;
        var to = BusinessTime.DayRangeUtc(request.To).ToUtc;
        var (sessions, visits) = await repository.GetAsync(from, to, request.DriverId, cancellationToken);
        return new(request.From, request.To, DriverControlAnalysis.Build(sessions, visits));
    }
}
