using FluentValidation;

namespace GMSoft.Application.Common.Validation;

public static class VisitDaysRules
{
    public static IRuleBuilderOptions<T, int[]?> ValidVisitDays<T>(this IRuleBuilder<T, int[]?> rule)
        => rule.Must(days => days is { Length: > 0 and <= 7 }
                    && days.All(day => day >= 1 && day <= 7)
                    && days.Distinct().Count() == days.Length)
            .WithMessage("Seleccioná al menos un día de visita, de lunes a domingo, sin repetir.");
}
