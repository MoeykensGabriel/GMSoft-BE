using GMSoft.Application.Common;
using GMSoft.Domain.Entities;

namespace GMSoft.Application.Features.Customers.Common;

/// <summary>
/// Umbrales globales de inactividad. No se miden en dias de calendario sino en
/// turnos perdidos: semanas en las que el camion salio a visitar al cliente y volvio
/// sin venderle. Una semana sin reparto no cuenta en su contra.
/// </summary>
public sealed class CustomerActivityPolicy
{
    public int RedAfterMissedWeeks { get; }
    public int BlackAfterMissedWeeks { get; }

    public CustomerActivityPolicy(int redAfterMissedWeeks, int blackAfterMissedWeeks)
    {
        if (redAfterMissedWeeks <= 0 || blackAfterMissedWeeks <= redAfterMissedWeeks)
            throw new ArgumentException("Los umbrales deben cumplir: 0 < rojo < negro.");

        RedAfterMissedWeeks = redAfterMissedWeeks;
        BlackAfterMissedWeeks = blackAfterMissedWeeks;
    }

    public static int DaysSince(DateTime sinceUtc, DateTime nowUtc)
        => Math.Max(0, (int)((nowUtc + BusinessTime.Offset).Date
            - (sinceUtc + BusinessTime.Offset).Date).TotalDays);

    public string GetStatus(int missedWeeks)
        => missedWeeks >= BlackAfterMissedWeeks ? "Black"
            : missedWeeks >= RedAfterMissedWeeks ? "Red" : "White";

    /// <summary>
    /// Semanas en que le toco comprar y no compro, desde su ultima compra (o desde el
    /// alta si nunca compro).
    ///
    /// Se cuenta por semana y no por visita: un cliente de lunes y jueves que no
    /// compro el lunes pero si el jueves no perdio nada, y dos salidas en la misma
    /// semana son un solo turno. Le "toco" cuando una salida ya recibida fue con su
    /// camion, su zona y alguno de sus dias. Se usa la asignacion actual del cliente.
    /// </summary>
    public static int MissedWeeks(
        Customer customer,
        DateTime? lastPurchaseAt,
        IEnumerable<RouteDeparture> closedDepartures)
    {
        // Sin camion o sin dias no entra en ningun recorrido: no tiene turnos que perder.
        if (customer.VehicleId is null || customer.VisitDays is not { Length: > 0 })
            return 0;

        // La semana de la compra esta cubierta; cuentan las siguientes.
        var primeraSemana = lastPurchaseAt is { } compra
            ? BusinessTime.WeekStart(compra).AddDays(7)
            : BusinessTime.WeekStart(customer.CreatedAt);

        return closedDepartures
            .Where(d => d.VehicleId == customer.VehicleId
                     && d.ZoneId == customer.ZoneId
                     && d.Days.Intersect(customer.VisitDays).Any()
                     // Una salida anterior al alta no pudo visitarlo.
                     && (lastPurchaseAt is not null || d.OpenedAt >= customer.CreatedAt))
            .Select(d => BusinessTime.WeekStart(d.OpenedAt))
            .Where(semana => semana >= primeraSemana)
            .Distinct()
            .Count();
    }
}

/// <summary>Una salida ya recibida, con lo minimo para saber a que clientes les toco.</summary>
public record RouteDeparture(Guid VehicleId, Guid ZoneId, int[] Days, DateTime OpenedAt);
