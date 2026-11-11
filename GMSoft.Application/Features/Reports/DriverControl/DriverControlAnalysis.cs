using GMSoft.Application.Common;
using GMSoft.Application.Features.Deliveries.Register;
using GMSoft.Domain.Enums;

namespace GMSoft.Application.Features.Reports.DriverControl;

/// <summary>Las cuentas del control de choferes, sin base de datos: se prueban solas.</summary>
public static class DriverControlAnalysis
{
    /// <summary>Entre dos casas distintas no se tarda menos que esto.</summary>
    public static readonly TimeSpan BurstGap = TimeSpan.FromSeconds(30);
    public const int BurstMinVisits = 5;

    public static IReadOnlyList<DriverControlDriverDto> Build(
        IReadOnlyList<DriverControlSessionRow> sessions, IReadOnlyList<DriverControlVisitRow> visits)
    {
        var bySession = visits.ToLookup(v => v.SessionId);
        return sessions.GroupBy(s => s.DriverId).Select(g =>
        {
            var rows = g.OrderBy(s => s.OpenedAt).ThenBy(s => s.Id)
                .Select(s => Session(s, bySession[s.Id].ToList())).ToList();
            var gaps = rows.SelectMany(r => Gaps(r.Marks)).ToList();
            return new DriverControlDriverDto(g.Key, g.First().DriverName, rows.Count,
                rows.Sum(r => r.Visits), rows.Sum(r => r.Bursts.Count), rows.Sum(r => r.Bursts.Sum(b => b.Count)),
                rows.Sum(r => r.OfflineVisits), rows.Sum(r => r.DoubtfulVisits), rows.Sum(r => r.OutsideHoursVisits),
                rows.Count(r => r.OpenedOutsideWorkingHours), rows.Sum(r => r.NoPurchase), rows.Sum(r => r.DidNotWant),
                Median(gaps), UsualOpening(rows.Select(r => r.OpenedAt)), rows);
        }).OrderBy(d => d.DriverName, StringComparer.CurrentCultureIgnoreCase).ThenBy(d => d.DriverId).ToList();
    }

    public static DriverControlSessionDto Session(DriverControlSessionRow s, IReadOnlyList<DriverControlVisitRow> visits)
    {
        var marks = visits.Select(v =>
        {
            var phone = v.MarkedAt is not null && !v.MarkedAtDoubtful;
            return new DriverControlMarkDto(phone ? v.MarkedAt!.Value : v.DeliveredAt, v.DeliveredAt, phone,
                v.CustomerName, v.Type, v.NoPurchaseReason,
                DeliveryMark.SentOffline(v.MarkedAt, v.MarkedAtDoubtful, v.DeliveredAt),
                v.MarkedAt is not null && v.MarkedAtDoubtful, false, v.IsOutsideWorkingHours);
        }).OrderBy(m => m.At).ThenBy(m => m.DeliveredAt).ThenBy(m => m.CustomerName).ToList();

        var bursts = new List<DriverControlBurstDto>();
        for (var start = 0; start < marks.Count;)
        {
            var end = start;
            while (end + 1 < marks.Count && marks[end + 1].At - marks[end].At <= BurstGap) end++;
            var count = end - start + 1;
            if (count >= BurstMinVisits)
            {
                bursts.Add(new(marks[start].At, marks[end].At, count,
                    marks.Skip(start).Take(count).Any(m => !m.PhoneTime)));
                for (var i = start; i <= end; i++) marks[i] = marks[i] with { InBurst = true };
            }
            start = end + 1;
        }

        int NoPurchase(NoPurchaseReason? reason) => marks.Count(m => m.Type == DeliveryType.NoPurchase &&
            (reason is null || m.NoPurchaseReason == reason));
        return new(s.Id, s.VehicleName, s.LicensePlate, s.ZoneName, s.Status, s.OpenedAt, s.OpenedOutsideWorkingHours,
            s.LoadedAt, s.ClosedAt, marks.FirstOrDefault()?.At, marks.LastOrDefault()?.At, marks.Count,
            Median(Gaps(marks)), marks.Count(m => m.Offline), marks.Count(m => m.Doubtful),
            marks.Count(m => m.OutsideWorkingHours), NoPurchase(null), NoPurchase(NoPurchaseReason.DidNotWant),
            bursts, marks);
    }

    static IEnumerable<double> Gaps(IReadOnlyList<DriverControlMarkDto> marks)
        => marks.Zip(marks.Skip(1), (a, b) => (b.At - a.At).TotalSeconds);

    static double? ExactMedian(IEnumerable<double> values)
    {
        var sorted = values.Order().ToList();
        if (sorted.Count == 0) return null;
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
    }

    static int? Median(IEnumerable<double> values) => ExactMedian(values) is { } m ? (int)Math.Round(m) : null;

    /// <summary>Al minuto, sin redondear: si abrió 09:31:40 se ve 09:31, como en la salida.</summary>
    static TimeOnly? UsualOpening(IEnumerable<DateTime> openings)
    {
        var minutes = openings.Select(o => o.Add(BusinessTime.Offset).TimeOfDay.TotalMinutes);
        return ExactMedian(minutes) is { } m ? TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(Math.Floor(m))) : null;
    }
}
