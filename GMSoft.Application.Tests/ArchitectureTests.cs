using GMSoft.Application.Extensions;
using GMSoft.Domain.Common;

namespace GMSoft.Application.Tests;

/// <summary>
/// Guardas de la regla de dependencias de Clean Architecture. Si alguien filtra
/// EF Core dentro de Application, o le cuelga una capa al Domain, estos tests
/// fallan antes de que el error se haga costumbre.
/// </summary>
public class ArchitectureTests
{
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
    public void Application_no_depende_de_EntityFrameworkCore()
    {
        var referencias = typeof(ApplicationLayerExtensions).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name!);

        Assert.DoesNotContain(
            referencias,
            n => n.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
              || n.StartsWith("Npgsql", StringComparison.Ordinal));
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
