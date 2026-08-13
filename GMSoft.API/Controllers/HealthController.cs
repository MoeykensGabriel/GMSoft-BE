using Microsoft.AspNetCore.Mvc;

namespace GMSoft.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    /// <summary>
    /// Ping sin dependencias — sirve para el health check de la plataforma de deploy.
    /// </summary>
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        status = "ok",
        utc    = DateTime.UtcNow
    });
}
