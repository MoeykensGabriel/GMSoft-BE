using System.Reflection;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Reports.VehiclePerformance;

namespace GMSoft.Application.Tests.Features.Reports;

public class VehiclePerformanceTests
{
    [Theory]
    [InlineData("2026-01-01", "2027-01-01", true)]
    [InlineData("2026-01-01", "2027-01-02", false)]
    [InlineData("2026-10-15", "2026-10-14", false)]
    [InlineData("0001-01-01", "0001-01-01", false)]
    [InlineData("9999-12-31", "9999-12-31", false)]
    [InlineData("2026-10-15", "2026-10-15", true)]
    [InlineData("9999-12-30", "9999-12-30", true)]
    public void Inclusive_range_is_bounded_and_safe(string from, string to, bool valid)
        => Assert.Equal(valid, new GetVehiclePerformanceQueryValidator()
            .Validate(new GetVehiclePerformanceQuery(DateOnly.Parse(from), DateOnly.Parse(to))).IsValid);

    [Fact]
    public async Task Handler_uses_argentine_boundaries_and_preserves_result()
    {
        var lines = new[] { Line(500, 650, 3, 2, 4, 25), Line(0, 0, 0, 0, 0, 0) };
        var repository = Stub<IVehiclePerformanceRepository>((_, args) =>
        {
            Assert.Equal(new DateTime(2026, 10, 14, 3, 0, 0, DateTimeKind.Utc), args![0]);
            Assert.Equal(new DateTime(2026, 10, 16, 3, 0, 0, DateTimeKind.Utc), args[1]);
            return Task.FromResult<IReadOnlyList<VehiclePerformanceLineDto>>(lines);
        });
        var result = await new GetVehiclePerformanceQueryHandler(repository, Stub<ICurrentUserService>((_, _) => true))
            .Handle(new(new(2026, 10, 14), new(2026, 10, 15)), default);
        Assert.Same(lines, result.Vehicles);
        Assert.Equal(new DateOnly(2026, 10, 14), result.From);
        Assert.Equal(new DateOnly(2026, 10, 15), result.To);
        Assert.Equal(-150m, lines[0].OnAccount);
        Assert.Equal(1.3m, lines[0].CollectedPercent);
        Assert.Equal(2m / 3, lines[0].BuyingPercent);
        Assert.Equal(125m, lines[0].AverageTicket);
        Assert.Equal(20m, lines[0].SoldPerKilometer);
        Assert.Null(lines[1].CollectedPercent);
        Assert.Null(lines[1].BuyingPercent);
        Assert.Null(lines[1].AverageTicket);
        Assert.Null(lines[1].SoldPerKilometer);
    }

    [Fact]
    public async Task Non_admin_is_rejected_before_repository_access()
        => await Assert.ThrowsAsync<ForbiddenException>(() => new GetVehiclePerformanceQueryHandler(
            Stub<IVehiclePerformanceRepository>((_, _) => throw new Exception("Unexpected read")),
            Stub<ICurrentUserService>((_, _) => false)).Handle(new(new(2026, 1, 1), new(2026, 1, 1)), default));

    private static VehiclePerformanceLineDto Line(decimal sold, decimal collected, int visited, int buyers, int sales, long km)
        => new(Guid.NewGuid(), "Camion", "AA123BB", [], 0, 0, sold, collected, new(0, 0, 0), 0,
            visited, buyers, 0, sales, new(0, new Dictionary<string, int>()), [], new(0, 0), 0, km);
    private static T Stub<T>(Func<string, object?[]?, object?> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, TestProxy>();
        ((TestProxy)(object)proxy).Handler = handler;
        return proxy;
    }
    public class TestProxy : DispatchProxy
    {
        public Func<string, object?[]?, object?> Handler { get; set; } = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!.Name, args);
    }
}
