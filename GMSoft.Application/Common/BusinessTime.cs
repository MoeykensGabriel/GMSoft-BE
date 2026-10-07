namespace GMSoft.Application.Common;

/// <summary>
/// El día del negocio, que no es el día UTC.
///
/// Todo se guarda en UTC, pero "el reparto del 31" es un día argentino. Filtrando
/// por día UTC, una salida que se cerró a las 21:30 cae recién en el día siguiente:
/// el reparto aparecería partido en dos, o directamente vacío.
///
/// El desfasaje es fijo porque Argentina no cambia de hora. Si algún día el negocio
/// opera en otro huso, esto pasa a ser configuración y deja de ser una constante.
/// </summary>
public static class BusinessTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(-3);

    public static int IsoDayOfWeek(DateTime utc)
        => ((int)utc.Add(Offset).DayOfWeek + 6) % 7 + 1;

    /// <summary>
    /// El lunes de la semana local en que cae ese instante. Dos instantes de la misma
    /// semana de reparto devuelven el mismo valor, que es lo que se compara.
    /// </summary>
    public static DateOnly WeekStart(DateTime utc)
        => DateOnly.FromDateTime(utc.Add(Offset)).AddDays(1 - IsoDayOfWeek(utc));

    /// <summary>
    /// El rango UTC que cubre ese día local, como [Desde, Hasta): se compara con
    /// "mayor o igual que Desde y menor que Hasta". Con un BETWEEN, el instante
    /// exacto de la medianoche caería en los dos días.
    /// </summary>
    public static (DateTime FromUtc, DateTime ToUtc) DayRangeUtc(DateOnly day)
    {
        var inicioLocal = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), Offset);
        var desde = inicioLocal.UtcDateTime;

        return (desde, desde.AddDays(1));
    }
}
