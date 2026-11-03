using GMSoft.Application.Common;

namespace GMSoft.Application.Features.Reports.VehicleKilometers;

/// <summary>
/// Arma los promedios de kilometraje a partir de las salidas cerradas. Es puro: el
/// repositorio solo trae filas y todo el criterio vive aca.
/// </summary>
public static class KilometerStatistics
{
    public static IReadOnlyList<VehicleKilometersLineDto> Build(IEnumerable<SessionKilometersRow> rows) => rows
        .GroupBy(r => r.VehicleId)
        .Select(g => Vehicle(g.OrderBy(r => r.OpenedAt).ToList()))
        .OrderBy(v => v.VehicleName, StringComparer.Create(new("es-AR"), true))
        .ThenBy(v => v.LicensePlate, StringComparer.Ordinal)
        .ToList();

    private static VehicleKilometersLineDto Vehicle(List<SessionKilometersRow> trips)
    {
        var km = trips.Select(Kilometers).ToList();
        var total = km.Sum(k => (long)k);
        var days = trips.Select(t => DateOnly.FromDateTime(t.OpenedAt + BusinessTime.Offset)).Distinct().Count();
        // Ordenadas por salida: lo que subio el tablero entre una vuelta y la salida siguiente.
        var between = trips.Zip(trips.Skip(1), (prev, next) => (long)next.KilometersAtOpen - prev.KilometersAtClose).Sum();
        var routes = trips.GroupBy(t => string.Join(" → ", t.Zones))
            .Select(r =>
            {
                var k = r.Select(Kilometers).ToList();
                return new RouteKilometersDto(r.Key, k.Count, Average(k), Median(k), k.Min(), k.Max());
            })
            .OrderByDescending(r => r.Sessions).ThenBy(r => r.Route, StringComparer.Ordinal)
            .ToList();
        var first = trips[0];
        return new(first.VehicleId, first.VehicleName, first.LicensePlate, km.Count, total,
            Average(km), Median(km), km.Min(), km.Max(), days, Round((decimal)total / days),
            km.Count(k => k == 0), between, routes);
    }

    private static int Kilometers(SessionKilometersRow r) => r.KilometersAtClose - r.KilometersAtOpen;

    private static decimal? Average(IReadOnlyCollection<int> km) =>
        km.Count == 0 ? null : Round(km.Sum(k => (decimal)k) / km.Count);

    /// <summary>Con cantidad par, el promedio de los dos del medio.</summary>
    public static decimal? Median(IReadOnlyCollection<int> km)
    {
        if (km.Count == 0) return null;
        var sorted = km.Order().ToList();
        var mid = sorted.Count / 2;
        return Round(sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + (decimal)sorted[mid]) / 2);
    }

    private static decimal Round(decimal value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);
}
