using GMSoft.Application.Common.Authorization;
using GMSoft.Data.Identity;
using Microsoft.AspNetCore.Identity;

namespace GMSoft.API.Services;

/// <summary>
/// Crea la cuenta de admin inicial si no existe. Sin esto no hay forma de entrar
/// al sistema la primera vez: los roles vienen sembrados por migración, pero no
/// los usuarios.
/// </summary>
public static class DatabaseSeeder
{
    public static async Task SeedAdminUserAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var userManager   = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger        = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        var email = configuration["Seed:AdminEmail"];
        // La contraseña nunca va en appsettings versionado: en local va en
        // appsettings.Development.json (ignorado por git) y en producción en variable
        // de entorno (Seed__AdminPassword).
        var password = Environment.GetEnvironmentVariable("SEED_ADMIN_PASSWORD")
                    ?? configuration["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "No hay Seed:AdminEmail o Seed:AdminPassword configurados — se omite la creación del admin inicial.");
            return;
        }

        if (await userManager.FindByEmailAsync(email) is not null)
            return;

        var admin = new ApplicationUser
        {
            UserName       = email,
            Email          = email,
            EmailConfirmed = true,
            FirstName      = configuration["Seed:AdminFirstName"] ?? "Admin",
            LastName       = configuration["Seed:AdminLastName"]  ?? string.Empty,
            IsActive       = true
        };

        var result = await userManager.CreateAsync(admin, password);

        if (!result.Succeeded)
        {
            logger.LogError(
                "No se pudo crear el admin inicial: {Errores}",
                string.Join(" | ", result.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(admin, AppRoles.Admin);
        logger.LogInformation("Admin inicial creado: {Email}", email);
    }
}
