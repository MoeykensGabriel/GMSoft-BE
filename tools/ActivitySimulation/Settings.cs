using Microsoft.Extensions.Configuration;
using Npgsql;

namespace ActivitySimulation;

public static class Settings
{
    public static (IConfigurationRoot Config, NpgsqlConnectionStringBuilder Connection) Load(string root)
    {
        var environments = new[] { Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT"),
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") }.Where(v => !string.IsNullOrWhiteSpace(v)).ToArray();
        if (environments.Length == 0 || environments.Any(v => !string.Equals(v, "Development", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Solo Development: definí ASPNETCORE_ENVIRONMENT=Development y quitá cualquier entorno contradictorio.");
        var config = new ConfigurationBuilder().SetBasePath(Path.Combine(root, "GMSoft.API"))
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables().Build();
        // Same precedence and URI conversion as DataLayerExtensions.ResolveConnectionString.
        var raw = new[] { Environment.GetEnvironmentVariable("CONNECTION_STRING"),
            Environment.GetEnvironmentVariable("DATABASE_URL"), Environment.GetEnvironmentVariable("DATABASE_PUBLIC_URL"),
            config.GetConnectionString("DefaultConnection") }.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        if (raw is null) throw new InvalidOperationException("Falta la conexión de desarrollo de la API.");
        NpgsqlConnectionStringBuilder connection;
        try
        {
            if (raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(raw);
                var info = uri.UserInfo.Split(':', 2);
                connection = new() { Host = uri.Host, Port = uri.Port > 0 ? uri.Port : 5432,
                    Username = Uri.UnescapeDataString(info[0]),
                    Password = info.Length > 1 ? Uri.UnescapeDataString(info[1]) : "",
                    Database = uri.AbsolutePath.TrimStart('/'), SslMode = SslMode.Prefer };
            }
            else connection = new(raw);
        }
        catch { throw new InvalidOperationException("La cadena de conexión tiene formato inválido (valor omitido)."); }
        if (!string.Equals(connection.Host, "localhost", StringComparison.OrdinalIgnoreCase) && connection.Host != "127.0.0.1")
            throw new InvalidOperationException("Solo se admite Host=localhost o Host=127.0.0.1; no se conectó a la base.");
        connection.IncludeErrorDetail = false;
        connection.Timeout = 5;
        return (config, connection);
    }
}
