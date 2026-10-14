using GMSoft.Application.Features.WorkingHours;

namespace GMSoft.Application.Common.Exceptions;

public class OutsideWorkingHoursException(WorkingHoursEvaluation evaluation)
    : ConflictException(Describe(evaluation))
{
    public const string ProblemType = "outside-working-hours";
    private static readonly string[] Dias =
        ["Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo"];

    private static string Describe(WorkingHoursEvaluation e)
    {
        var d = e.Day!;
        var dia = Dias[d.DayOfWeek - 1];
        return d.IsWorkingDay
            ? $"Fuera del horario laboral. {dia}: {d.From:HH:mm} a {d.Until:HH:mm}; límite para abrir salidas: {d.OpeningCutoff ?? d.Until:HH:mm} (Argentina, UTC-3)."
            : $"Fuera del horario laboral. {dia}: no laborable (Argentina, UTC-3).";
    }
}
