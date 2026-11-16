using GMSoft.API.Modules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GMSoft.API.Controllers;

/// <summary>Los módulos extra de esta instalación. El panel lo usa para armar el menú.</summary>
[ApiController]
[Route("api/modules")]
[Authorize]
public class ModulesController(InstallationModules modules) : ControllerBase
{
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public ActionResult<IReadOnlyList<string>> Get() => Ok(modules.Enabled);
}
