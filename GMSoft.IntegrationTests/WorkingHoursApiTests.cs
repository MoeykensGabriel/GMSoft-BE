using System.Reflection;
using System.Text.Json;
using GMSoft.API.Controllers;
using GMSoft.API.Middleware;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Features.Sessions.Open;
using GMSoft.Application.Features.WorkingHours;
using GMSoft.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace GMSoft.IntegrationTests;

// Contrato HTTP sin levantar un servidor: middleware real y metadatos MVC.
public class WorkingHoursApiTests
{
    [Fact]
    public async Task Outside_hours_is_409_ProblemDetails_with_stable_type_and_daily_hours()
    {
        using var services = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();
        context.Request.Path = "/api/sessions/open";
        var evaluation = new WorkingHoursEvaluation(new(2026, 10, 12, 22, 0, 0, DateTimeKind.Utc), true,
            new WorkingDay(1, true, new(8, 0), new(18, 0), new(16, 0)));
        await new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance)
            .TryHandleAsync(context, new OutsideWorkingHoursException(evaluation), default);
        Assert.Equal(409, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("outside-working-hours", json.RootElement.GetProperty("type").GetString());
        Assert.Equal(409, json.RootElement.GetProperty("status").GetInt32());
        var detail = json.RootElement.GetProperty("detail").GetString();
        Assert.Contains("08:00", detail); Assert.Contains("18:00", detail); Assert.Contains("16:00", detail);
        Assert.Contains("UTC-3", detail);
    }

    [Fact]
    public void Settings_require_authentication_and_only_admin_can_write()
    {
        Assert.NotNull(typeof(WorkingHoursController).GetCustomAttribute<AuthorizeAttribute>());
        Assert.Null(typeof(WorkingHoursController).GetMethod("Get")!.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal("Admin", typeof(WorkingHoursController).GetMethod("Put")!.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
        Assert.Equal("Admin,Driver", typeof(SessionsController).GetMethod("Open")!.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
    }

    [Fact]
    public void Existing_open_body_defaults_to_no_override_and_no_selected_driver()
    {
        var command = JsonSerializer.Deserialize<OpenSessionCommand>(
            """{"zoneId":"11111111-1111-4111-8111-111111111111","kilometersAtOpen":100}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.False(command.ConfirmOutsideWorkingHours); Assert.Null(command.DriverId);
    }
}
