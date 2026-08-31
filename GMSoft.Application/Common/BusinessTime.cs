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
