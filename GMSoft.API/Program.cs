using Microsoft.EntityFrameworkCore;
using GMSoft.API.Middleware;
using GMSoft.Application.Extensions;
using GMSoft.Data.Context;
using GMSoft.Data.Extensions;
using Serilog;

// Bootstrap logger — captura errores antes de que el DI container esté listo
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

// Npgsql: acepta DateTime con Kind Unspecified/Local en columnas timestamptz.
// Sin esto, cualquier fecha que llegue del frontend en JSON (que deserializa como
// Unspecified) revienta al guardar. Las fechas se siguen guardando en UTC.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Serilog — reemplaza el logging de .NET con configuración desde appsettings
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "GMSoft"));

// Data Layer (PostgreSQL + repositorios + FluentValidation)
builder.Services.AddDataLayer(builder.Configuration);

// Application Layer (MediatR + ValidationBehaviour + FluentValidation + Mapster)
builder.Services.AddApplicationLayer();

// CORS — permite requests desde el frontend.
// Orígenes por env var CORS_ORIGINS (prod, CSV) o Cors:AllowedOrigins en
// appsettings; fallback a localhost para dev.
var allowedOrigins =
    (Environment.GetEnvironmentVariable("CORS_ORIGINS")
    ?? builder.Configuration["Cors:AllowedOrigins"]
    ?? "http://localhost:3000")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Global exception handler → ProblemDetails (RFC 7807)
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Middleware pipeline (el orden importa)
app.UseExceptionHandler();
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} → {StatusCode} ({Elapsed:0.0}ms)";
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "GMSoft API v1");
        options.RoutePrefix = string.Empty;
    });
}

// HTTPS redirect solo en dev: en producción el proxy de la plataforma termina TLS
// y nos pasa HTTP — redirigir ahí genera loops/warnings.
if (app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseCors("FrontendPolicy");
app.MapControllers();

// Auto-migración en Development, o en producción si Database:MigrateOnStartup=true
// (env var Database__MigrateOnStartup — sin consola interactiva, migrar al arrancar
// es el paso de deploy). Es idempotente: si está al día no hace nada.
// GetMigrations() lee el assembly, no la DB: mientras no exista la primera migración
// la app arranca sin necesidad de tener Postgres levantado.
var migrateOnStartup = app.Configuration.GetValue<bool>("Database:MigrateOnStartup");
if (app.Environment.IsDevelopment() || migrateOnStartup)
{
    using var scope = app.Services.CreateScope();
    var dbContext   = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger      = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    if (dbContext.Database.GetMigrations().Any())
    {
        try
        {
            var pending = await dbContext.Database.GetPendingMigrationsAsync();
            var pendingList = pending.ToList();
            if (pendingList.Count > 0)
            {
                logger.LogInformation("Aplicando {Count} migración(es) pendiente(s)...", pendingList.Count);
                await dbContext.Database.MigrateAsync();
                logger.LogInformation("Migraciones aplicadas correctamente.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fallo aplicando migraciones automáticas.");
            throw; // No queremos seguir si la DB no está bien.
        }
    }
    else
    {
        logger.LogWarning("Todavía no hay migraciones en GMSoft.Data — se omite la migración automática.");
    }
}

app.Run();
