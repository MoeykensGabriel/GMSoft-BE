using GMSoft.Application.Common;
using GMSoft.Domain.Entities;

namespace GMSoft.Application.Features.VehicleLoads.Common;

public static class RouteDaySelection
{
    public static int[] Resolve(IEnumerable<VehicleLoad> loads, DateTime fallbackUtc)
    {
        var days = loads.SelectMany(load => load.RouteDays ?? []).Distinct().Order().ToArray();
        return days.Length > 0 ? days : [BusinessTime.IsoDayOfWeek(fallbackUtc)];
    }
}
