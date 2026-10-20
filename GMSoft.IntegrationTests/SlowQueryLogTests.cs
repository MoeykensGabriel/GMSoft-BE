using GMSoft.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GMSoft.IntegrationTests;

/// <summary>
/// Las consultas que pasan el umbral quedan en el log con el SQL, pero sin los valores
/// de los parámetros: ahí van datos de clientes.
/// </summary>
[Collection("PostgreSQL")]
public class SlowQueryLogTests(PostgresFixture fixture)
{
    private sealed class CapturingLogger : ILogger<SlowQueryInterceptor>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, formatter(state, exception)));
    }

    private async Task<CapturingLogger> RunCustomerQuery(TimeSpan umbral)
    {
        using var scope = fixture.Services.CreateScope();
        var connection = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetConnectionString();
        var logger = new CapturingLogger();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection)
            .AddInterceptors(new SlowQueryInterceptor(logger, umbral)).Options;
        await using var db = new AppDbContext(options);
        // En variable, como llega un dato de cliente en la app: EF lo manda como parámetro.
        var telefono = "3815550001";
        await db.Customers.CountAsync(c => c.Phone == telefono);
        return logger;
    }

    [Fact]
    public async Task Una_consulta_que_pasa_el_umbral_se_anota_sin_los_valores()
    {
        var logger = await RunCustomerQuery(TimeSpan.Zero);

        var (level, message) = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, level);
        Assert.Contains("Consulta lenta", message);
        Assert.Contains("\"Customers\"", message);
        Assert.DoesNotContain("3815550001", message);
    }

    [Fact]
    public async Task Debajo_del_umbral_no_se_anota_nada()
    {
        var logger = await RunCustomerQuery(TimeSpan.FromMinutes(1));
        Assert.Empty(logger.Entries);
    }
}
