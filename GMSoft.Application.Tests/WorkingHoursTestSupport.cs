using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.WorkingHours;
using GMSoft.Domain.Entities;

namespace GMSoft.Application.Tests;

internal static class WorkingHoursTestSupport
{
    public static WorkingHoursService Unrestricted() => new(new Settings(), TimeProvider.System);
    internal sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    internal sealed class Settings : IWorkingHoursRepository
    {
        public WorkingHoursSettings? Value { get; set; }
        public int Reads { get; private set; }
        public Task<WorkingHoursSettings?> GetAsync(CancellationToken ct) { Reads++; return Task.FromResult(Value); }
        public Task SetAsync(WorkingDay[] days, CancellationToken ct)
        { Value = new() { Days = days }; return Task.CompletedTask; }
    }
}
