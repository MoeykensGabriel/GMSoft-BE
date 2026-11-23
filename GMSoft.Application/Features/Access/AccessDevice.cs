using System.Text.RegularExpressions;
using GMSoft.Domain.Enums;

namespace GMSoft.Application.Features.Access;

/// <summary>Resume el User-Agent en algo que la oficina entienda: sistema y navegador.</summary>
public static partial class AccessDevice
{
    public static string? Describe(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return null;
        var system = Match(AndroidRegex(), userAgent, "Android ")
            ?? (userAgent.Contains("iPhone") ? "iPhone" : userAgent.Contains("iPad") ? "iPad"
            : userAgent.Contains("Windows") ? "Windows" : userAgent.Contains("Mac OS") ? "Mac"
            : userAgent.Contains("Linux") ? "Linux" : null);
        var browser = Embedded(userAgent)
            ?? Match(SamsungRegex(), userAgent, "Samsung Internet ")
            ?? Match(EdgeRegex(), userAgent, "Edge ")
            ?? Match(FirefoxRegex(), userAgent, "Firefox ")
            ?? Match(ChromeRegex(), userAgent, "Chrome ")
            ?? (userAgent.Contains("Safari") ? "Safari" : null);
        var text = string.Join(" · ", new[] { system, browser }.Where(p => p is not null));
        return text.Length == 0 ? userAgent[..Math.Min(userAgent.Length, 120)] : text;
    }

    /// <summary>Desde dónde entró: el ícono instalado, el navegador o el navegador de otra app.</summary>
    public static AccessClient Client(string? userAgent, bool? installed)
        => Embedded(userAgent) is not null ? AccessClient.EmbeddedBrowser
            : installed == true ? AccessClient.InstalledApp
            : installed == false ? AccessClient.Browser
            : AccessClient.Unknown;

    private static string? Embedded(string? userAgent)
    {
        if (string.IsNullOrEmpty(userAgent)) return null;
        if (userAgent.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase)) return "dentro de WhatsApp";
        if (userAgent.Contains("Instagram", StringComparison.OrdinalIgnoreCase)) return "dentro de Instagram";
        if (userAgent.Contains("FBAN") || userAgent.Contains("FBAV") || userAgent.Contains("FB_IAB")) return "dentro de Facebook";
        // "; wv)" marca la vista web de Android que usan otras apps para abrir enlaces.
        return userAgent.Contains("; wv)") ? "vista web de otra app" : null;
    }

    private static string? Match(Regex regex, string text, string prefix)
        => regex.Match(text) is { Success: true } m ? prefix + m.Groups[1].Value : null;

    [GeneratedRegex(@"Android (\d+)")] private static partial Regex AndroidRegex();
    [GeneratedRegex(@"SamsungBrowser/(\d+)")] private static partial Regex SamsungRegex();
    [GeneratedRegex(@"Edg(?:A|iOS)?/(\d+)")] private static partial Regex EdgeRegex();
    [GeneratedRegex(@"(?:Firefox|FxiOS)/(\d+)")] private static partial Regex FirefoxRegex();
    [GeneratedRegex(@"(?:Chrome|CriOS)/(\d+)")] private static partial Regex ChromeRegex();
}
