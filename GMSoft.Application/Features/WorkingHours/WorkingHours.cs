using FluentValidation;
using GMSoft.Application.Common;
using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.WorkingHours;

public record WorkingHoursDto(bool IsConfigured, IReadOnlyList<WorkingDay> Days);
public record GetWorkingHoursQuery : IRequest<WorkingHoursDto>;
public class GetWorkingHoursQueryHandler(IWorkingHoursRepository repository)
    : IRequestHandler<GetWorkingHoursQuery, WorkingHoursDto>
{
    public async Task<WorkingHoursDto> Handle(GetWorkingHoursQuery request, CancellationToken ct)
    {
        var settings = await repository.GetAsync(ct);
        return new(settings is not null, settings?.Days.OrderBy(d => d.DayOfWeek).ToArray() ?? []);
    }
}

public record UpdateWorkingHoursCommand(WorkingDay[] Days) : IRequest<WorkingHoursDto>;
public class UpdateWorkingHoursCommandValidator : AbstractValidator<UpdateWorkingHoursCommand>
{
    public UpdateWorkingHoursCommandValidator()
    {
        RuleFor(x => x.Days).NotNull();
        When(x => x.Days is not null, () =>
        {
            RuleFor(x => x.Days).Must(days => days.Length == 7 && days.All(d => d is not null) &&
                days.Select(d => d.DayOfWeek).Order().SequenceEqual(Enumerable.Range(1, 7)))
                .WithMessage("Debe indicar los siete días ISO, sin repetir (lunes=1, domingo=7).");
            RuleForEach(x => x.Days).NotNull().ChildRules(day =>
            {
                day.RuleFor(d => d).Must(d => d.IsWorkingDay
                    ? d.From.HasValue && d.Until.HasValue && d.From < d.Until &&
                        (!d.OpeningCutoff.HasValue || d.OpeningCutoff >= d.From && d.OpeningCutoff <= d.Until)
                    : d.From is null && d.Until is null && d.OpeningCutoff is null)
                    .WithMessage("Día laborable: desde < hasta y límite dentro del rango. Día no laborable: horas nulas.");
                day.RuleFor(d => d).Must(d => new[] { d.From, d.Until, d.OpeningCutoff }
                    .All(t => !t.HasValue || t.Value.Ticks % TimeSpan.TicksPerMinute == 0))
                    .WithMessage("Las horas deben tener precisión de minutos.");
            });
        });
    }
}
public class UpdateWorkingHoursCommandHandler(IWorkingHoursRepository repository, ICurrentUserService user)
    : IRequestHandler<UpdateWorkingHoursCommand, WorkingHoursDto>
{
    public async Task<WorkingHoursDto> Handle(UpdateWorkingHoursCommand request, CancellationToken ct)
    {
        if (!user.IsInRole(AppRoles.Admin)) throw new ForbiddenException("Solo ADMIN puede configurar el horario laboral.");
        var days = request.Days.OrderBy(d => d.DayOfWeek).ToArray();
        await repository.SetAsync(days, ct);
        return new(true, days);
    }
}

public record WorkingHoursEvaluation(DateTime UtcNow, bool IsOutsideWorkingHours, WorkingDay? Day);

/// <summary>Única regla de horario. Evalúa y fecha la operación con el mismo instante del servidor.</summary>
public class WorkingHoursService(IWorkingHoursRepository repository, TimeProvider clock)
{
    public async Task<WorkingHoursEvaluation> EvaluateAsync(CancellationToken ct, bool opening = false)
    {
        var utc = clock.GetUtcNow().UtcDateTime;
        utc = new DateTime(utc.Ticks - utc.Ticks % 10, DateTimeKind.Utc);
        var settings = await repository.GetAsync(ct);
        if (settings is null) return new(utc, false, null);
        var day = settings.Days.Single(d => d.DayOfWeek == BusinessTime.IsoDayOfWeek(utc));
        var time = TimeOnly.FromDateTime(utc.Add(BusinessTime.Offset));
        // Desde inclusive, hasta exclusive. El límite de apertura es inclusive.
        var outside = !day.IsWorkingDay || time < day.From || time >= day.Until ||
            (opening && time > (day.OpeningCutoff ?? day.Until));
        return new(utc, outside, day);
    }

    public async Task<WorkingHoursEvaluation> EnsureAllowedAsync(bool opening, bool confirm,
        ICurrentUserService user, CancellationToken ct)
    {
        var result = await EvaluateAsync(ct, opening);
        if (result.IsOutsideWorkingHours && !(confirm && user.IsInRole(AppRoles.Admin)))
            throw new OutsideWorkingHoursException(result);
        return result;
    }
}
