using GMSoft.Application.Common;

namespace GMSoft.Application.Features.Customers.Common;

/// <summary>Plazos globales, medidos desde la ultima compra o el alta si nunca compro.</summary>
public sealed class CustomerActivityPolicy
{
    public int RedAfterDays { get; }
    public int BlackAfterDays { get; }

    public CustomerActivityPolicy(int redAfterDays, int blackAfterDays)
    {
        if (redAfterDays <= 0 || blackAfterDays <= redAfterDays)
            throw new ArgumentException("Los plazos deben cumplir: 0 < rojo < negro.");

        RedAfterDays = redAfterDays;
        BlackAfterDays = blackAfterDays;
    }

    public static int DaysSince(DateTime sinceUtc, DateTime nowUtc)
        => Math.Max(0, (int)((nowUtc + BusinessTime.Offset).Date
            - (sinceUtc + BusinessTime.Offset).Date).TotalDays);

    public string GetStatus(int days)
        => days >= BlackAfterDays ? "Black" : days >= RedAfterDays ? "Red" : "White";
}
