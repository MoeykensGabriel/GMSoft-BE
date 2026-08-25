using GMSoft.Application.Extensions;
using GMSoft.Domain.Common;

namespace GMSoft.Application.Tests;

/// <summary>
/// Guardas de la regla de dependencias de Clean Architecture. Si alguien filtra
/// infraestructura dentro de Application, o le cuelga una capa al Domain, estos
/// tests fallan antes de que el error se haga costumbre.
/// </summary>
public class ArchitectureTests
{
    /// <summary>
    /// Paquetes de infraestructura que Application no puede tocar. EF Core y Npgsql
    /// son persistencia; Identity es autenticación. Los tres viven en Data, y
    /// Application solo conoce las interfaces.
    /// </summary>
    private static readonly string[] InfraestructuraProhibida =
    [
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "Microsoft.AspNetCore.Identity"
    ];

    [Fact]
    public void Domain_no_depende_de_ninguna_otra_capa()
    {
        var referencias = typeof(BaseEntity).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => n.StartsWith("GMSoft.", StringComparison.Ordinal));

        Assert.Empty(referencias);
    }

    [Fact]
    public void Application_no_depende_de_la_infraestructura()
    {
        var referencias = typeof(ApplicationLayerExtensions).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .ToList();

        var filtradas = referencias
            .Where(n => InfraestructuraProhibida.Any(p => n.StartsWith(p, StringComparison.Ordinal)))
            .ToList();

        Assert.Empty(filtradas);
    }

    [Fact]
    public void Application_solo_referencia_al_Domain_entre_las_capas_propias()
    {
        var referencias = typeof(ApplicationLayerExtensions).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => n.StartsWith("GMSoft.", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(["GMSoft.Domain"], referencias);
    }
}
