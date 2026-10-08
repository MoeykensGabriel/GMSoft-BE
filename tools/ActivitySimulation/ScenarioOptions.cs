using System.Globalization;
using GMSoft.Application.Common;

namespace ActivitySimulation;

public sealed record ScenarioOptions(string Scenario, string[] Arguments)
{
    public static ScenarioOptions Parse(string[] args)
    {
        string? scenario = null;
        var rest = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] != "--scenario") { rest.Add(args[i]); continue; }
            if (scenario is not null || ++i >= args.Length || args[i] is not ("activity" or "first-visit"))
                throw new InvalidOperationException("Indicá --scenario activity o --scenario first-visit una sola vez.");
            scenario = args[i];
        }
        return new(scenario ?? "activity", rest.ToArray());
    }
}

public sealed record FirstVisitOptions(string Mode, DateOnly Date, int Count, string? Zone)
{
    public static FirstVisitOptions Parse(string[] args, DateTime nowUtc)
    {
        var today = DateOnly.FromDateTime(nowUtc.Add(BusinessTime.Offset));
        var date = today.AddDays(-7);
        var count = 3;
        string? zone = null;
        var mode = "--dry-run";
        var seen = new HashSet<string>();
        for (var i = 0; i < args.Length; i++)
        {
            var key = args[i];
            if (!seen.Add(key)) throw new InvalidOperationException($"Argumento repetido: {key}.");
            if (key is "--dry-run" or "--apply" or "--undo")
            {
                if (seen.Count(k => k is "--dry-run" or "--apply" or "--undo") > 1)
                    throw new InvalidOperationException("Indicá un solo modo: --dry-run, --apply o --undo.");
                mode = key;
                continue;
            }
            if (key is not ("--date" or "--count" or "--zone"))
                throw new InvalidOperationException("first-visit admite [--dry-run|--apply|--undo] [--date YYYY-MM-DD] [--count 1..10] [--zone ID_o_nombre].");
            if (++i >= args.Length || string.IsNullOrWhiteSpace(args[i]) || args[i].StartsWith("--"))
                throw new InvalidOperationException($"Falta el valor de {key}.");
            var value = args[i];
            if (key == "--date" && !DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                throw new InvalidOperationException("--date requiere una fecha válida YYYY-MM-DD.");
            if (key == "--count" && (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out count) || count is < 1 or > 10))
                throw new InvalidOperationException("--count debe estar entre 1 y 10.");
            if (key == "--zone") zone = value.Trim();
        }
        if (date >= today) throw new InvalidOperationException("--date debe ser anterior a hoy en hora argentina.");
        if (mode == "--undo" && seen.Any(k => k is "--date" or "--count" or "--zone"))
            throw new InvalidOperationException("--undo usa exclusivamente el manifiesto, sin --date, --count ni --zone.");
        return new(mode, date, count, zone);
    }
}
