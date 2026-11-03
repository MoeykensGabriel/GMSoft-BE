using GMSoft.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GMSoft.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    /// <summary>
    /// Ping sin dependencias — sirve para el health check de la plataforma de deploy.
    /// </summary>
    [HttpGet, HttpHead]
    [AllowAnonymous]
    public IActionResult Get() => Ok(new
    {
        status = "ok",
        utc    = DateTime.UtcNow
    });

    /// <summary>
    /// Si la API llega a la base. Es para el monitor externo: el de arriba queda sin
    /// dependencias para que un corte breve de la base no haga fallar un despliegue.
    /// No detalla el error: la ruta es pública. Acepta HEAD porque es lo que manda el
    /// monitor gratis, y sin eso cae en la política por defecto y responde 401.
    /// </summary>
    [HttpGet("db"), HttpHead("db")]
    [AllowAnonymous]
    public async Task<IActionResult> Database([FromServices] IDatabaseProbe database, CancellationToken cancellationToken)
    {
        var connected = await database.CanConnectAsync(cancellationToken);
        var body = new { status = connected ? "ok" : "error", utc = DateTime.UtcNow };
        return connected ? Ok(body) : StatusCode(StatusCodes.Status503ServiceUnavailable, body);
    }
}
