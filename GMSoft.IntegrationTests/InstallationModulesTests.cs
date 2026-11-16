using System.Reflection;
using GMSoft.API.Controllers;
using GMSoft.API.Modules;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace GMSoft.IntegrationTests;

// Los planes (PLANES.md): qué lee la instalación y qué endpoints corta el módulo apagado.
public class InstallationModulesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("base")]
    [InlineData(" Base ")]
    public void Without_extras_the_installation_is_the_base_plan(string? configured)
    {
        var modules = new InstallationModules(configured);
        Assert.Empty(modules.Enabled);
        Assert.Empty(modules.Unknown);
        Assert.False(modules.IsEnabled(InstallationModules.Control));
    }

    [Theory]
    [InlineData("control")]
    [InlineData(" Control ")]
    [InlineData("base,control")]
    [InlineData("control,control")]
    public void The_control_module_is_enabled_by_name(string configured)
    {
        var modules = new InstallationModules(configured);
        Assert.Equal([InstallationModules.Control], modules.Enabled);
        Assert.Empty(modules.Unknown);
    }

    [Fact]
    public void A_misspelled_module_is_ignored_and_reported()
    {
        var modules = new InstallationModules("contrl");
        Assert.Empty(modules.Enabled);
        Assert.Equal(["contrl"], modules.Unknown);
    }

    [Theory]
    [InlineData("base", 403)]
    [InlineData("control", null)]
    public void The_filter_rejects_with_403_only_when_the_module_is_off(string configured, int? expectedStatus)
    {
        using var services = new ServiceCollection().AddSingleton(new InstallationModules(configured)).BuildServiceProvider();
        var context = new ResourceExecutingContext(
            new ActionContext(new DefaultHttpContext { RequestServices = services }, new RouteData(), new ActionDescriptor()),
            [], []);

        new RequiresModuleAttribute(InstallationModules.Control).OnResourceExecuting(context);

        if (expectedStatus is null) { Assert.Null(context.Result); return; }
        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(expectedStatus, result.StatusCode);
        Assert.Equal(RequiresModuleAttribute.ProblemType, Assert.IsType<ProblemDetails>(result.Value).Type);
    }

    [Fact]
    public void Only_the_endpoints_exclusive_to_the_module_are_gated()
    {
        static string? ModuleOf(string action) =>
            typeof(ReportsController).GetMethod(action)!.GetCustomAttribute<RequiresModuleAttribute>()?.Module;

        Assert.Equal(InstallationModules.Control, ModuleOf("DriverControl"));
        Assert.Equal(InstallationModules.Control, ModuleOf("VehicleKilometers"));
        // Ventas (plan base) usa el mismo endpoint que Estadísticas: no se puede cortar.
        Assert.Null(ModuleOf("VehiclePerformance"));
        Assert.Null(ModuleOf("RouteSheet"));
    }
}
