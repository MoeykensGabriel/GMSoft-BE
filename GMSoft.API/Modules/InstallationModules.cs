namespace GMSoft.API.Modules;

/// <summary>
/// Los módulos extra que contrató esta instalación (ver PLANES.md). Se leen una vez al
/// arrancar, de la variable MODULES: "base" (o vacío) es solo el plan base; los extras
/// van separados por coma, por ejemplo "control".
/// </summary>
public sealed class InstallationModules
{
    /// <summary>Control de choferes, Control de vehículos y Estadísticas.</summary>
    public const string Control = "control";

    /// <summary>Todos los módulos extra que existen. Un módulo nuevo se suma acá.</summary>
    public static readonly IReadOnlyList<string> Known = [Control];

    /// <summary>Los que tiene esta instalación, en el orden de <see cref="Known"/>.</summary>
    public IReadOnlyList<string> Enabled { get; }

    /// <summary>Lo que decía la variable y no es ningún módulo: se avisa en el log al arrancar.</summary>
    public IReadOnlyList<string> Unknown { get; }

    public InstallationModules(string? configured)
    {
        var names = (configured ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(name => name.ToLowerInvariant())
            .Where(name => name != "base")
            .Distinct()
            .ToList();

        Enabled = Known.Where(names.Contains).ToList();
        Unknown = names.Except(Known).ToList();
    }

    public bool IsEnabled(string module) => Enabled.Contains(module);
}
