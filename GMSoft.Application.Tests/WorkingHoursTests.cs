using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Features.WorkingHours;
using GMSoft.Domain.Entities;

namespace GMSoft.Application.Tests;

public class WorkingHoursTests
{
    internal static WorkingDay[] Week() => Enumerable.Range(1, 7)
        .Select(d => d == 7 ? new WorkingDay(d, false, null, null) :
            new WorkingDay(d, true, new(8, 0), new(18, 0), new(16, 0))).ToArray();

    [Theory]
    [InlineData("2026-10-12T10:59:59Z", false, true)]
    [InlineData("2026-10-12T11:00:00Z", false, false)]
    [InlineData("2026-10-12T20:59:59Z", false, false)]
    [InlineData("2026-10-12T21:00:00Z", false, true)]
    [InlineData("2026-10-12T19:00:00Z", true, false)]
    [InlineData("2026-10-12T19:00:01Z", true, true)]
    [InlineData("2026-10-12T19:00:01Z", false, false)]
    [InlineData("2026-10-11T15:00:00Z", false, true)]
    [InlineData("2026-10-12T02:59:59Z", true, true)]
    public async Task Server_utc_is_evaluated_in_Argentina_with_distinct_opening_cutoff(string utc, bool opening, bool outside)
    {
        var clock = new WorkingHoursTestSupport.Clock(DateTimeOffset.Parse(utc));
        var settings = new WorkingHoursTestSupport.Settings { Value = new() { Days = Week() } };
        var result = await new WorkingHoursService(settings, clock).EvaluateAsync(default, opening);
        Assert.Equal(outside, result.IsOutsideWorkingHours);
        Assert.Equal(clock.Now.UtcDateTime, result.UtcNow);
        if (utc == "2026-10-12T02:59:59Z") Assert.Equal(7, result.Day!.DayOfWeek);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task No_configuration_is_unrestricted_and_read_does_not_create_it(bool opening)
    {
        var settings = new WorkingHoursTestSupport.Settings();
        var clock = new WorkingHoursTestSupport.Clock(DateTimeOffset.Parse("2026-10-11T03:00:00Z"));
        Assert.False((await new WorkingHoursService(settings, clock).EvaluateAsync(default, opening)).IsOutsideWorkingHours);
        var dto = await new GetWorkingHoursQueryHandler(settings).Handle(new(), default);
        Assert.False(dto.IsConfigured); Assert.Empty(dto.Days); Assert.Null(settings.Value);
    }

    [Fact]
    public async Task Omitted_cutoff_uses_until()
    {
        var days = Week(); days[0] = days[0] with { OpeningCutoff = null };
        var settings = new WorkingHoursTestSupport.Settings { Value = new() { Days = days } };
        var clock = new WorkingHoursTestSupport.Clock(DateTimeOffset.Parse("2026-10-12T20:59:59Z"));
        var service = new WorkingHoursService(settings, clock);
        Assert.False((await service.EvaluateAsync(default, true)).IsOutsideWorkingHours);
        clock.Now = clock.Now.AddSeconds(1);
        Assert.True((await service.EvaluateAsync(default, true)).IsOutsideWorkingHours);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public async Task Only_explicit_admin_confirmation_overrides(bool admin, bool confirm, bool allowed)
    {
        var settings = new WorkingHoursTestSupport.Settings { Value = new() { Days = Week() } };
        var service = new WorkingHoursService(settings,
            new WorkingHoursTestSupport.Clock(DateTimeOffset.Parse("2026-10-11T15:00:00Z")));
        if (allowed) Assert.True((await service.EnsureAllowedAsync(false, confirm, new User(admin), default)).IsOutsideWorkingHours);
        else
        {
            var error = await Assert.ThrowsAsync<OutsideWorkingHoursException>(() =>
                service.EnsureAllowedAsync(false, confirm, new User(admin), default));
            Assert.Contains("no laborable", error.Message);
            Assert.Equal("outside-working-hours", OutsideWorkingHoursException.ProblemType);
        }
    }

    [Theory]
    [InlineData("null")][InlineData("missing")][InlineData("duplicate")][InlineData("null-day")]
    [InlineData("equal")][InlineData("overnight")][InlineData("early-cutoff")][InlineData("late-cutoff")]
    [InlineData("missing-time")][InlineData("non-working-time")][InlineData("seconds")]
    public void Rejects_invalid_week(string invalid)
    {
        var days = Week();
        switch (invalid)
        {
            case "null": days = null!; break;
            case "missing": days = days[..6]; break;
            case "duplicate": days[0] = days[1]; break;
            case "null-day": days[0] = null!; break;
            case "equal": days[0] = days[0] with { Until = new(8, 0) }; break;
            case "overnight": days[0] = days[0] with { Until = new(7, 0) }; break;
            case "early-cutoff": days[0] = days[0] with { OpeningCutoff = new(7, 0) }; break;
            case "late-cutoff": days[0] = days[0] with { OpeningCutoff = new(19, 0) }; break;
            case "missing-time": days[0] = days[0] with { From = null }; break;
            case "non-working-time": days[6] = days[6] with { From = new(8, 0) }; break;
            case "seconds": days[0] = days[0] with { From = new(8, 0, 1) }; break;
        }
        Assert.False(new UpdateWorkingHoursCommandValidator().Validate(new UpdateWorkingHoursCommand(days)).IsValid);
    }

    [Fact]
    public async Task Valid_week_replaces_configuration_and_driver_cannot_edit()
    {
        var settings = new WorkingHoursTestSupport.Settings();
        var request = new UpdateWorkingHoursCommand(Week().Reverse().ToArray());
        Assert.True(new UpdateWorkingHoursCommandValidator().Validate(request).IsValid);
        await Assert.ThrowsAsync<ForbiddenException>(() => new UpdateWorkingHoursCommandHandler(settings, new User(false)).Handle(request, default));
        Assert.Null(settings.Value);
        var dto = await new UpdateWorkingHoursCommandHandler(settings, new User(true)).Handle(request, default);
        Assert.True(dto.IsConfigured); Assert.Equal(Enumerable.Range(1, 7), dto.Days.Select(d => d.DayOfWeek));
    }

    private sealed class User(bool admin) : ICurrentUserService
    {
        public Guid? UserId => Guid.Parse("11111111-1111-4111-8111-111111111111");
        public Guid? DriverId => null;
        public string? UserName => "test";
        public string? Email => null;
        public bool IsInRole(string role) => role == (admin ? "Admin" : "Driver");
    }
}
