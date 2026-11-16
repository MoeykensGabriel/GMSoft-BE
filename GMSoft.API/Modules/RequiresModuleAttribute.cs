using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GMSoft.API.Modules;

/// <summary>
/// El endpoint pertenece a un módulo extra: si la instalación no lo contrató, responde
/// 403 sin ejecutar nada. Va endpoint por endpoint y no en el controlador, porque los
/// controladores mezclan funciones del plan base con las del módulo.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequiresModuleAttribute(string module) : Attribute, IResourceFilter
{
    public const string ProblemType = "module-disabled";

    public string Module { get; } = module;

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var modules = context.HttpContext.RequestServices.GetRequiredService<InstallationModules>();
        if (modules.IsEnabled(Module)) return;

        context.Result = new ObjectResult(new ProblemDetails
        {
            Type   = ProblemType,
            Title  = "Forbidden",
            Detail = "Esta función no está incluida en el plan contratado.",
            Status = StatusCodes.Status403Forbidden
        })
        { StatusCode = StatusCodes.Status403Forbidden };
    }

    public void OnResourceExecuted(ResourceExecutedContext context) { }
}
