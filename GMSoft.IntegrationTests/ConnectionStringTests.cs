using GMSoft.Data.Context;
using GMSoft.Data.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace GMSoft.IntegrationTests;

[CollectionDefinition("Connection configuration", DisableParallelization = true)]
public sealed class ConnectionConfigurationCollection;

[Collection("Connection configuration")]
public sealed class ConnectionStringTests
{
    [Theory]
    [InlineData("", SslMode.Prefer)]
    [InlineData("?sslmode=disable", SslMode.Disable)]
    [InlineData("?sslmode=allow", SslMode.Allow)]
    [InlineData("?sslmode=prefer", SslMode.Prefer)]
    [InlineData("?sslmode=require", SslMode.Require)]
    [InlineData("?sslmode=verify-ca", SslMode.VerifyCA)]
    [InlineData("?sslmode=verify-full", SslMode.VerifyFull)]
    public void Railway_uri_preserves_connection_fields_and_ssl_mode(string query, SslMode expected)
    {
        WithConnection("postgresql://test_user@postgres.railway.internal:5432/railway" + query, () =>
        {
            var parsed = new NpgsqlConnectionStringBuilder(ResolveThroughDataLayer());
            Assert.Equal("postgres.railway.internal", parsed.Host);
            Assert.Equal(5432, parsed.Port);
            Assert.Equal("railway", parsed.Database);
            Assert.Equal("test_user", parsed.Username);
            Assert.Equal(expected, parsed.SslMode);
        });
    }

    [Fact]
    public void Invalid_ssl_mode_is_rejected_instead_of_silently_downgraded()
    {
        WithConnection("postgresql://test_user@postgres.railway.internal/railway?sslmode=invalid", () =>
            Assert.Throws<ArgumentException>(() => ResolveThroughDataLayer()));
    }

    [Fact]
    public void Native_connection_string_is_unchanged()
    {
        const string connection = "Host=localhost;Database=railway;Username=test_user;SSL Mode=Disable";
        WithConnection(connection, () => Assert.Equal(connection, ResolveThroughDataLayer()));
    }

    private static string ResolveThroughDataLayer()
    {
        var services = new ServiceCollection();
        services.AddDataLayer(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetConnectionString()!;
    }

    private static void WithConnection(string connection, Action assertion)
    {
        var previous = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        try
        {
            Environment.SetEnvironmentVariable("CONNECTION_STRING", connection);
            assertion();
        }
        finally
        {
            Environment.SetEnvironmentVariable("CONNECTION_STRING", previous);
        }
    }
}
