using GMSoft.Application.Features.Customers.ActivitySettings;
using System.Data.Common;
using System.Diagnostics;
using System.Xml.Linq;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Extensions;
using GMSoft.Application.Features.Customers.Common;
using GMSoft.Data.Context;
using GMSoft.Data.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace GMSoft.IntegrationTests;

[CollectionDefinition("PostgreSQL", DisableParallelization = true)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;

public sealed class TestClock : TimeProvider
{
    public DateTimeOffset? Now { get; set; }
    public override DateTimeOffset GetUtcNow() => Now ?? TimeProvider.System.GetUtcNow();
}

public sealed class TestUser : ICurrentUserService
{
    public Guid? UserId { get; set; }
    public Guid? DriverId { get; set; }
    public bool Admin { get; set; }
    public string? UserName => "integration";
    public string? Email => null;
    public bool IsInRole(string role) => role == (Admin ? "Admin" : "Driver");
}

// Only synchronizes real SELECTs; it never replaces a repository, result or transaction.
// Both PostgreSQL stock queries finish before either handler can write.
public sealed class SqlReadBarrier(string table = "SessionStockMovements", string clause = "GROUP BY") : DbCommandInterceptor
{
    private int arrivals;
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,
        CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        if (command.CommandText.StartsWith("SELECT") && command.CommandText.Contains(table) && command.CommandText.Contains(clause))
        {
            if (Interlocked.Increment(ref arrivals) == 2) ready.TrySetResult();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
        }
        return result;
    }
}

public sealed class ScopeInstrumentation
{
    public SqlReadBarrier? ReadBarrier { get; set; }
}

public sealed class PostgresFixture : IAsyncLifetime
{
    public string DatabaseName { get; } = "gmsoft_it_" + Guid.NewGuid().ToString("N")[..16];
    public ServiceProvider Services { get; private set; } = null!;
    private string adminConnection = "";
    private bool created;
    private readonly Stopwatch elapsed = new();

    public static void EnsureTestDatabase(string name)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^gmsoft_it_[a-z0-9_]+$"))
            throw new InvalidOperationException("Operacion prohibida: la base debe tener prefijo gmsoft_it_.");
    }

    public async Task InitializeAsync()
    {
        elapsed.Start();
        try
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GMSoft.slnx")))
                directory = directory.Parent;
            var api = Path.Combine(directory?.FullName ?? throw new InvalidOperationException("No se encontro GMSoft.slnx."), "GMSoft.API");
            var configuration = new ConfigurationBuilder().SetBasePath(api)
                .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Development.json", optional: true);
            var secretsId = XDocument.Load(Path.Combine(api, "GMSoft.API.csproj"))
                .Descendants("UserSecretsId").FirstOrDefault()?.Value;
            if (secretsId is not null) configuration.AddUserSecrets(secretsId);
            var config = configuration.AddEnvironmentVariables().Build();
            static string? NonEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
            var raw = NonEmpty(Environment.GetEnvironmentVariable("GMSOFT_TEST_CONNECTION"))
                ?? NonEmpty(Environment.GetEnvironmentVariable("CONNECTION_STRING"))
                ?? NonEmpty(Environment.GetEnvironmentVariable("DATABASE_URL"))
                ?? NonEmpty(Environment.GetEnvironmentVariable("DATABASE_PUBLIC_URL"))
                ?? config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Sin configuracion PostgreSQL.");
            NpgsqlConnectionStringBuilder source;
            if (raw.StartsWith("postgres://") || raw.StartsWith("postgresql://"))
            {
                var uri = new Uri(raw);
                var credentials = uri.UserInfo.Split(':', 2);
                source = new() { Host = uri.Host, Port = uri.Port > 0 ? uri.Port : 5432,
                    Username = Uri.UnescapeDataString(credentials[0]),
                    Password = credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : "", SslMode = SslMode.Prefer };
            }
            else source = new(raw);
            // Never reuse the source database, search path, passfile or arbitrary options.
            var connection = new NpgsqlConnectionStringBuilder { Host = source.Host, Port = source.Port,
                Username = source.Username, Password = source.Password, SslMode = source.SslMode,
                Database = "postgres", Pooling = false, Timeout = 5, CommandTimeout = 30,
                IncludeErrorDetail = false, ApplicationName = "GMSoft.IntegrationTests" };
            adminConnection = connection.ConnectionString;
            EnsureTestDatabase(DatabaseName);
            await using (var admin = new NpgsqlConnection(adminConnection))
            {
                await admin.OpenAsync();
                await using var command = new NpgsqlCommand($"CREATE DATABASE \"{DatabaseName}\"", admin);
                await command.ExecuteNonQueryAsync();
                created = true;
            }
            connection.Database = DatabaseName;
            var testConnection = connection.ConnectionString;
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(config);
            services.AddDataLayer(config);
            services.AddApplicationLayer();
            services.AddScoped<TestClock>();
            services.AddScoped<TimeProvider>(sp => sp.GetRequiredService<TestClock>());
            services.AddCustomerActivityPolicy(
                config.GetValue<int>("CustomerActivity:RedAfterMissedWeeks"),
                config.GetValue<int>("CustomerActivity:BlackAfterMissedWeeks"));
            services.AddScoped<TestUser>();
            services.AddScoped<ICurrentUserService>(sp => sp.GetRequiredService<TestUser>());
            services.AddScoped<ScopeInstrumentation>();
            // Last registration wins. AddDataLayer reads process-level CONNECTION_STRING;
            // explicitly replace the context factory, without changing process environment.
            services.AddScoped(sp =>
            {
                EnsureTestDatabase(new NpgsqlConnectionStringBuilder(testConnection).Database!);
                var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(testConnection);
                if (sp.GetRequiredService<ScopeInstrumentation>().ReadBarrier is { } barrier)
                    options.AddInterceptors(barrier);
                return new AppDbContext(options.Options);
            });
            Services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
            Console.WriteLine($"PostgreSQL: creada y migrada {DatabaseName}.");
        }
        catch (Exception error)
        {
            await DisposeAsync();
            // Do not include the source string or an exception that could contain credentials.
            throw new InvalidOperationException("No se pudo preparar PostgreSQL real. Configura GMSOFT_TEST_CONNECTION " +
                "(o CONNECTION_STRING) con un servidor alcanzable y permiso CREATEDB. " +
                $"Fase de inicializacion fallida ({error.GetType().Name}).");
        }
    }

    public async Task DisposeAsync()
    {
        if (Services is not null) await Services.DisposeAsync();
        if (!created) return;
        EnsureTestDatabase(DatabaseName);
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using (var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{DatabaseName}\" WITH (FORCE)", admin))
            await drop.ExecuteNonQueryAsync();
        created = false;
        await using var list = new NpgsqlCommand("SELECT datname FROM pg_database WHERE starts_with(datname, 'gmsoft_it_') ORDER BY datname", admin);
        await using var reader = await list.ExecuteReaderAsync();
        var remaining = new List<string>();
        while (await reader.ReadAsync()) remaining.Add(reader.GetString(0));
        Console.WriteLine($"PostgreSQL: eliminada {DatabaseName}; bases gmsoft_it_* restantes: " +
            (remaining.Count == 0 ? "ninguna" : string.Join(", ", remaining)) + $"; tiempo fixture: {elapsed.Elapsed.TotalSeconds:F2}s.");
        Assert.DoesNotContain(DatabaseName, remaining);
    }
}
