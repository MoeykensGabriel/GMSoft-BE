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
        _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);

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

            // Optimistic concurrency desde EF. Se traduce a 409 con mensaje genérico
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

            _ => (
                StatusCodes.Status500InternalServerError,
                new ProblemDetails
                {
                    Title  = "Internal Server Error",
                    Detail = "An unexpected error occurred. Please try again later.",
                    Status = StatusCodes.Status500InternalServerError
                })
        };

        problemDetails.Instance = httpContext.Request.Path;

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
