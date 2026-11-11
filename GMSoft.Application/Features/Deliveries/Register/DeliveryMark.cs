namespace GMSoft.Application.Features.Deliveries.Register;

/// <summary>
/// La hora del celular de una visita. No se confía a ciegas: un reloj mal puesto la deja
/// en el futuro o antes de abrir la salida. Se guarda igual y queda marcada como dudosa.
/// </summary>
public static class DeliveryMark
{
    /// <summary>Margen para relojes de celular apenas corridos respecto del servidor.</summary>
    public static readonly TimeSpan ClockTolerance = TimeSpan.FromMinutes(2);

    /// <summary>Desde esta demora entre el toque y la llegada, la visita se envió sin señal.</summary>
    public static readonly TimeSpan OfflineDelay = TimeSpan.FromMinutes(5);

    public static (DateTime? MarkedAt, bool Doubtful) Evaluate(DateTime? markedAt, DateTime deliveredAtUtc, DateTime openedAtUtc)
    {
        if (markedAt is not { } value) return (null, false);
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
        // Misma precisión que guarda Postgres (microsegundos).
        utc = new DateTime(utc.Ticks - utc.Ticks % 10, DateTimeKind.Utc);
        var doubtful = utc > deliveredAtUtc + ClockTolerance || utc < openedAtUtc - ClockTolerance;
        return (utc, doubtful);
    }

    /// <summary>La visita llegó al servidor bastante después de marcarse: el celular no tenía señal.</summary>
    public static bool SentOffline(DateTime? markedAt, bool doubtful, DateTime deliveredAt)
        => markedAt is { } m && !doubtful && deliveredAt - m > OfflineDelay;
}
