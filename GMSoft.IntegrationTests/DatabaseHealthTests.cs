using GMSoft.API.Controllers;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Data.Context;
using GMSoft.Data.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class DatabaseHealthTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Con_la_base_disponible_responde_ok()
    {
        var f = await new Scenario(fixture).Seed(open: false);
        using var scope = f.Scope();
        var result = await new HealthController().Database(scope.ServiceProvider.GetRequiredService<IDatabaseProbe>(), default);
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Sin_la_base_responde_503_sin_lanzar()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=nada;Username=nadie;Password=nada;Timeout=2").Options;
        await using var db = new AppDbContext(options);
        var result = await new HealthController().Database(new DatabaseProbe(db, NullLogger<DatabaseProbe>.Instance), default);
        Assert.Equal(503, Assert.IsType<ObjectResult>(result).StatusCode);
    }
}
