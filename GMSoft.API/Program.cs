using GMSoft.Application.Features.Customers.ActivitySettings;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using GMSoft.API.Middleware;
using GMSoft.API.Services;
using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Extensions;
using GMSoft.Application.Features.Customers.Common;
using GMSoft.Data.Context;
using GMSoft.Data.Extensions;
using Serilog;

// Bootstrap logger — captura errores antes de que el DI container esté listo
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

// Serilog — reemplaza el logging de .NET con configuración desde appsettings
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "GMSoft"));

// Data Layer (PostgreSQL + Identity + repositorios + servicios de auth)
builder.Services.AddDataLayer(builder.Configuration);

// Application Layer (MediatR + ValidationBehaviour + FluentValidation + Mapster)
builder.Services.AddApplicationLayer();

// Se valida al iniciar para no servir estados con plazos inconsistentes.
// ADMIN puede cambiarlos desde Configuración; estos valen mientras no lo haga.
builder.Services.AddCustomerActivityPolicy(
    builder.Configuration.GetValue<int>("CustomerActivity:RedAfterMissedWeeks"),
    builder.Configuration.GetValue<int>("CustomerActivity:BlackAfterMissedWeeks"));

// Usuario actual leído de los claims del JWT
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Autenticación JWT
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey   = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") ?? jwtSettings["SecretKey"];

if (string.IsNullOrWhiteSpace(secretKey))
    throw new InvalidOperationException(
        "Falta la clave de firma del JWT. Configura JwtSettings:SecretKey en appsettings.Development.json " +
        "o la variable de entorno JWT_SECRET_KEY.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // Sin remapeo: los claims conservan los nombres cortos con los que se emitieron
    // (sub, email, role) en lugar de las URIs largas de WS-Federation.
    options.MapInboundClaims = false;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer           = true,
        ValidateAudience         = true,
        ValidateLifetime         = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer              = jwtSettings["Issuer"],
        ValidAudience            = jwtSettings["Audience"],
        IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew                = TimeSpan.Zero,

        // Sin esto, [Authorize(Roles = ...)] e IsInRole no encuentran los roles,
        // porque buscan el claim con el nombre por defecto y no "role".
        RoleClaimType            = AppClaimTypes.Role,
        NameClaimType            = AppClaimTypes.UserName
    };

    // El token dura horas: si ADMIN desactiva al chofer o le cambia la contraseña, el
    // próximo pedido ya se rechaza con 401 y la app lo manda al login.
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var identity = context.HttpContext.RequestServices.GetRequiredService<IIdentityService>();
            var activa = Guid.TryParse(context.Principal?.FindFirst(AppClaimTypes.UserId)?.Value, out var userId)
                && await identity.IsTokenValidAsync(userId,
                    context.Principal?.FindFirst(AppClaimTypes.SecurityStamp)?.Value, context.HttpContext.RequestAborted);
            if (!activa) context.Fail("La cuenta está desactivada o cambió su contraseña.");
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    // Todo endpoint pide autenticación salvo que diga [AllowAnonymous] explícitamente.
    // Al revés — abierto por defecto — alcanza con olvidarse un atributo para dejar
    // expuesto un endpoint nuevo.
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Límite de intentos de login por IP: 60 por minuto. Una persona no llega nunca,
// aunque varios choferes entren desde el mismo wifi; solo frena a un programa que
// pruebe contraseñas sin parar. Las cuentas no se bloquean por errores.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window      = TimeSpan.FromMinutes(1),
                QueueLimit  = 0
            }));
});

// CORS — permite requests desde el frontend.
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

// Detrás del proxy de Railway la conexión llega siempre desde el proxy: sin esto
// todos los usuarios comparten una IP y el límite de login los bloquea a todos
// juntos. Railway documenta X-Real-IP como la IP del cliente (no documenta cómo arma
// X-Forwarded-For), así que se lee esa; ForwardLimit = 1 toma un solo valor.
// Railway define RAILWAY_ENVIRONMENT; fuera de un proxy queda apagado.
var behindProxy = builder.Configuration.GetValue<bool?>("ForwardedHeaders:Enabled")
    ?? !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RAILWAY_ENVIRONMENT"));
if (behindProxy)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardedForHeaderName = "X-Real-IP";
        options.ForwardLimit = 1;
        // Las IP del proxy de Railway no son fijas: se confía en el salto inmediato.
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

// El celular del chofer consulta seguido con datos móviles: el JSON comprimido pesa
// una fracción. Fastest porque el ahorro es casi el mismo y la CPU también se paga.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);

// Global exception handler → ProblemDetails (RFC 7807)
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Controllers + Swagger con soporte para el token
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        // Los enums viajan como texto ("ByBalance") y no como número. Un 1 en el JSON
        // no le dice nada a nadie, y si algún día se reordena el enum los datos viejos
        // cambian de significado sin que nada falle.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "GMSoft API",
        Version     = "v1",
        Description = "Backend de reparto de agua, sifones y dispensers"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Pega el token que devuelve /api/auth/login."
    });

    // El requisito se arma con el documento en mano: la referencia al esquema
    // "Bearer" necesita saber en qué documento vive para resolverse.
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer", document, null), new List<string>() }
    });
});

var app = builder.Build();

// Middleware pipeline (el orden importa)
if (behindProxy) app.UseForwardedHeaders(); // Antes que todo: el límite de login necesita la IP real.
app.UseResponseCompression(); // Así también se comprimen los errores.
app.Use((context, next) =>
{
    // La API solo devuelve JSON: que ningún navegador intente interpretarlo como otra cosa.
    context.Response.Headers.XContentTypeOptions = "nosniff";
    return next(context);
});
app.UseExceptionHandler();
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} -> {StatusCode} ({Elapsed:0.0}ms)";
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
// y nos pasa HTTP — redirigir ahí genera loops y warnings.
if (app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseCors("FrontendPolicy");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Migración automática y seed del admin. GetMigrations() lee el assembly y no la
// base, así que mientras no exista la primera migración la app levanta sin Postgres.
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
            // Si la base todavia no existe, GetPendingMigrationsAsync no puede leer el
            // historial de migraciones y falla con un error de conexion que no dice lo
            // que pasa. MigrateAsync la crea y aplica todo de una.
            if (!await dbContext.Database.CanConnectAsync())
            {
                logger.LogInformation("La base no existe todavia. Creandola y aplicando migraciones...");
                await dbContext.Database.MigrateAsync();
                logger.LogInformation("Base creada y migraciones aplicadas.");
            }
            else
            {
                var pendingList = (await dbContext.Database.GetPendingMigrationsAsync()).ToList();
                if (pendingList.Count > 0)
                {
                    logger.LogInformation("Aplicando {Count} migracion(es) pendiente(s)...", pendingList.Count);
                    await dbContext.Database.MigrateAsync();
                    logger.LogInformation("Migraciones aplicadas correctamente.");
                }
            }

            await DatabaseSeeder.SeedAdminUserAsync(app.Services);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fallo aplicando migraciones automaticas o sembrando el admin.");
            throw; // No queremos seguir si la DB no está bien.
        }
    }
    else
    {
        logger.LogWarning("Todavia no hay migraciones en GMSoft.Data — se omiten la migracion y el seed.");
    }
}

app.Run();
