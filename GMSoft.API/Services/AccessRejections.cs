using System.Security.Claims;
using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Features.Access;
using GMSoft.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace GMSoft.API.Services;

/// <summary>
/// Anota en el control de sesiones cuándo el servidor sacó a un chofer: token vencido,
/// contraseña o usuario cambiados, cuenta desactivada. Nunca puede cambiar la respuesta
/// del pedido: si algo falla, se registra en el log y sigue.
/// </summary>
public static class AccessRejections
{
    /// <summary>Token con firma válida, pero la cuenta ya no lo acepta.</summary>
    public static Task RecordAsync(HttpContext http, ClaimsPrincipal? principal, AccessEndReason reason)
        => RecordAsync(http, principal?.FindFirst(AppClaimTypes.UserId)?.Value,
            principal?.FindFirst(AppClaimTypes.DriverId)?.Value,
            principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value, reason);

    /// <summary>
    /// Token vencido. Antes de anotarlo se vuelve a validar la firma sin mirar la fecha:
    /// un token inventado no puede llenar el control de eventos falsos.
    /// </summary>
    public static async Task RecordExpiredAsync(AuthenticationFailedContext context)
    {
        try
        {
            var header = context.Request.Headers.Authorization.ToString();
            if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return;
            var parameters = context.Options.TokenValidationParameters.Clone();
            parameters.ValidateLifetime = false;
            var result = await new JsonWebTokenHandler().ValidateTokenAsync(header["Bearer ".Length..].Trim(), parameters);
            if (!result.IsValid) return;
            await RecordAsync(context.HttpContext, result.ClaimsIdentity.FindFirst(AppClaimTypes.UserId)?.Value,
                result.ClaimsIdentity.FindFirst(AppClaimTypes.DriverId)?.Value,
                result.ClaimsIdentity.FindFirst(JwtRegisteredClaimNames.Jti)?.Value, AccessEndReason.Expired);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log(context.HttpContext, ex);
        }
    }

    private static async Task RecordAsync(HttpContext http, string? userId, string? driverId, string? tokenId,
        AccessEndReason reason)
    {
        // Solo se controla a los choferes.
        if (!Guid.TryParse(userId, out var user) || !Guid.TryParse(driverId, out var driver)) return;
        try
        {
            await http.RequestServices.GetRequiredService<ISender>().Send(new RecordAccessRejectionCommand(user, driver,
                tokenId, reason, http.Request.Headers.UserAgent.ToString()), http.RequestAborted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Dos pedidos con el mismo token pueden chocar en el índice único: ya quedó anotado.
            Log(http, ex);
        }
    }

    private static void Log(HttpContext http, Exception ex)
        => http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(AccessRejections))
            .LogWarning(ex, "No se pudo anotar el rechazo de acceso");
}
