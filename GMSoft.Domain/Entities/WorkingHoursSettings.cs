namespace GMSoft.Domain.Entities;

/// <summary>Una sola configuración; su ausencia significa que no hay restricciones.</summary>
public class WorkingHoursSettings
{
    public int Id { get; set; } = 1;
    public WorkingDay[] Days { get; set; } = [];
}

/// <summary>Día ISO: lunes=1, domingo=7. Los días no laborables no tienen horas.</summary>
public record WorkingDay(int DayOfWeek, bool IsWorkingDay, TimeOnly? From,
    TimeOnly? Until, TimeOnly? OpeningCutoff = null);
