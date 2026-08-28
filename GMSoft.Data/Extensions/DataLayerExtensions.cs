using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Data.Context;
using GMSoft.Data.Identity;
using GMSoft.Data.Repositories;
using GMSoft.Data.Services;

namespace GMSoft.Data.Extensions;

public static class DataLayerExtensions
{
    public static IServiceCollection AddDataLayer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // PostgreSQL + EF Core
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                ResolveConnectionString(configuration),
                npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)
            )
        );

        // ASP.NET Identity
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequiredLength         = 8;
            options.Password.RequireDigit           = true;
            options.Password.RequireUppercase       = true;
            options.Password.RequireNonAlphanumeric = false;
            options.User.RequireUniqueEmail         = true;

            // Anti fuerza bruta: tras 5 fallos la cuenta queda bloqueada 15 minutos.
            // El conteo lo lleva IdentityService en el login.
            options.Lockout.AllowedForNewUsers      = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan  = TimeSpan.FromMinutes(15);
        })
        .AddRoles<ApplicationRole>()
        .AddEntityFrameworkStores<AppDbContext>();

        // Unit of Work — AppDbContext ya es Scoped, lo exponemos como IUnitOfWork
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());

        // Repositorio genérico. Los repositorios específicos por agregado se registran
        // debajo a medida que aparecen.
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        // Repositorios por agregado
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IDriverRepository, DriverRepository>();
        services.AddScoped<IZoneRepository, ZoneRepository>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();

        // Autenticación
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IIdentityService, IdentityService>();

        // FluentValidation — registra todos los validators del assembly de Data
        services.AddValidatorsFromAssembly(typeof(DataLayerExtensions).Assembly);

        return services;
    }

    /// <summary>
    /// Resuelve la connection string Postgres desde las variables que puede exponer
    /// Railway (CONNECTION_STRING / DATABASE_URL / DATABASE_PUBLIC_URL) o appsettings
    /// (DefaultConnection) en local. Railway la entrega como URI (postgresql://...),
    /// formato que Npgsql NO parsea: se convierte a key=value con SslMode.Prefer
    /// (negocia SSL — lo exige el proxy público y funciona en la red interna).
    /// </summary>
    private static string? ResolveConnectionString(IConfiguration configuration)
    {
        static string? NonEmpty(string? v) => string.IsNullOrWhiteSpace(v) ? null : v;

        var raw = NonEmpty(Environment.GetEnvironmentVariable("CONNECTION_STRING"))
               ?? NonEmpty(Environment.GetEnvironmentVariable("DATABASE_URL"))
               ?? NonEmpty(Environment.GetEnvironmentVariable("DATABASE_PUBLIC_URL"))
               ?? NonEmpty(configuration.GetConnectionString("DefaultConnection"));

        if (string.IsNullOrWhiteSpace(raw))
            return null; // EF tirará su error estándar de connection string faltante

        // Ya viene en formato key=value (local) → sin cambios.
        if (!raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return raw;

        var uri      = new Uri(raw);
        var userInfo = uri.UserInfo.Split(':', 2);

        var csb = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host     = uri.Host,
            Port     = uri.Port > 0 ? uri.Port : 5432,
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty,
            Database = uri.AbsolutePath.TrimStart('/'),
            SslMode  = Npgsql.SslMode.Prefer,
        };

        return csb.ConnectionString;
    }
}
