using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GMSoft.Application.Common.Exceptions;

namespace GMSoft.API.Middleware;

/// <summary>
/// Intercepta todas las excepciones no manejadas y las convierte en respuestas
/// ProblemDetails (RFC 7807) con el HTTP status code correcto.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    /// <summary>
    /// Convencion de nginx para "el cliente corto la conexion". No es estandar, pero
    /// no hay codigo HTTP para esto y de todos modos no queda nadie del otro lado
    /// para leerlo: sirve para que en las metricas no se mezcle con los 500 reales.
    /// </summary>
    private const int ClientClosedRequest = 499;

    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // El chofer trabaja en la calle con señal mala: que se le corte un request a
        // la mitad es lo normal, no una falla del sistema. Sin este caso, cada tunel
        // y cada ascensor escriben un ERROR en el log y inflan la tasa de error.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation(
                "El cliente corto la conexion antes de que terminara {Method} {Path}.",
                httpContext.Request.Method,
                httpContext.Request.Path);

            if (!httpContext.Response.HasStarted)
                httpContext.Response.StatusCode = ClientClosedRequest;

            return true;
        }

        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        var (statusCode, problemDetails) = exception switch
        {
            NotFoundException e => (
                StatusCodes.Status404NotFound,
                new ProblemDetails
                {
                    Title  = "Not Found",
                    Detail = e.Message,
                    Status = StatusCodes.Status404NotFound
                }),

            ValidationException e => (
                StatusCodes.Status400BadRequest,
                (ProblemDetails)new ValidationProblemDetails(e.Errors)
                {
                    Title  = "Validation Failed",
                    Detail = e.Message,
                    Status = StatusCodes.Status400BadRequest
                }),

            ConflictException e => (
                StatusCodes.Status409Conflict,
                new ProblemDetails
                {
                    Title  = "Conflict",
                    Detail = e.Message,
                    Status = StatusCodes.Status409Conflict
                }),

            // Optimistic concurrency desde EF. Se traduce a 409 con mensaje generico
            // (el FE puede refrescar la pantalla afectada al ver el código).
            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                new ProblemDetails
                {
                    Title  = "Conflict",
                    Detail = "Otro usuario modificó este recurso en simultáneo. Refrescá la pantalla y volvé a intentar.",
                    Status = StatusCodes.Status409Conflict
                }),

            BadRequestException e => (
                StatusCodes.Status400BadRequest,
                new ProblemDetails
                {
                    Title  = "Bad Request",
                    Detail = e.Message,
                    Status = StatusCodes.Status400BadRequest
                }),

            ForbiddenException e => (
                StatusCodes.Status403Forbidden,
                new ProblemDetails
                {
                    Title  = "Forbidden",
                    Detail = e.Message,
                    Status = StatusCodes.Status403Forbidden
                }),

            UnauthorizedException e => (
                StatusCodes.Status401Unauthorized,
                new ProblemDetails
                {
                    Title  = "Unauthorized",
                    Detail = e.Message,
                    Status = StatusCodes.Status401Unauthorized
                }),

            // Todo lo demas es un bug nuestro. El detalle no viaja al cliente: puede
            // tener nombres de tablas, rutas o fragmentos de query.
            _ => (
                StatusCodes.Status500InternalServerError,
                new ProblemDetails
                {
                    Title  = "Internal Server Error",
                    Detail = "Ocurrió un error inesperado. Volvé a intentar en un momento.",
                    Status = StatusCodes.Status500InternalServerError
                })
        };

        // Un 404 o un 409 son resultados esperados del negocio, no fallas. Loguearlos
        // como error llena el archivo de ruido y esconde los 500, que son los unicos
        // que hay que mirar.
        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                exception,
                "Error no manejado en {Method} {Path}. TraceId {TraceId}",
                httpContext.Request.Method, httpContext.Request.Path, traceId);
        }
        else
        {
            _logger.LogInformation(
                "{Method} {Path} rechazado con {StatusCode}: {Motivo}. TraceId {TraceId}",
                httpContext.Request.Method, httpContext.Request.Path,
                statusCode, exception.Message, traceId);
        }

        problemDetails.Instance = httpContext.Request.Path;

        // El mismo identificador viaja al cliente y queda en el log. Sin esto, un
        // "me dio error" no se puede cruzar contra ninguna linea del archivo.
        problemDetails.Extensions["traceId"] = traceId;

        // Si la respuesta ya empezó a escribirse no se pueden cambiar headers ni
        // cuerpo; intentarlo tira otra excepcion encima de la original y tapa la real.
        if (httpContext.Response.HasStarted)
        {
            _logger.LogWarning(
                "La respuesta ya habia empezado: no se pudo devolver el ProblemDetails. TraceId {TraceId}",
                traceId);
            return true;
        }

        httpContext.Response.StatusCode = statusCode;

        // Se serializa con el tipo REAL y no con el declarado. System.Text.Json mira
        // el tipo estatico, que aca es ProblemDetails, y con eso un
        // ValidationProblemDetails pierde su diccionario Errors: el cliente recibe el
        // 400 pero sin saber que campo esta mal, que es justo lo unico que necesita.
        await httpContext.Response.WriteAsJsonAsync(
            problemDetails, problemDetails.GetType(), cancellationToken);

        return true;
    }
}
