using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GMSoft.Data.Context;

/// <summary>
/// Contexto para las herramientas de EF en tiempo de diseño (dotnet ef migrations).
/// Sin esto, EF levanta el host completo de la API solo para leer el modelo, y
/// se choca con la validacion del secreto del JWT que corre en el arranque.
///
/// Generar una migracion no abre conexion: solo construye el modelo, asi que la
/// connection string de aca es un placeholder. Las migraciones se aplican al
/// arrancar la API, que si lee la configuracion real.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=GMSoftDb;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .Options;

        return new AppDbContext(options);
    }
}
