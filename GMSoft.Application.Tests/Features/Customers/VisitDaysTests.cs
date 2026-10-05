using System.Reflection;
using GMSoft.Application.Common;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Customers.Common;
using GMSoft.Application.Features.Customers.Create;
using GMSoft.Application.Features.Customers.Update;
using GMSoft.Domain.Entities;

namespace GMSoft.Application.Tests.Features.Customers;

public class VisitDaysTests
{
    public static IEnumerable<object?[]> InvalidDays => [
        [null], [Array.Empty<int>()], [new[] { 0 }], [new[] { 8 }], [new[] { -1 }], [new[] { 1, 1 }]
    ];

    private static CreateCustomerCommand Create(int[]? days) => new(null, "Ana", "123", "Calle 1", null, Guid.NewGuid(), null, days);
    private static UpdateCustomerCommand Update(Customer customer, Guid zone, int[]? days) => new(customer.Id, null, "Ana", "123", "Calle 1", null, zone, null, null, true, days);

    [Theory]
    [MemberData(nameof(InvalidDays))]
    public void Admin_must_supply_valid_distinct_days(int[]? days)
    {
        Assert.Contains(new CreateCustomerCommandValidator().Validate(Create(days)).Errors, error => error.PropertyName == "VisitDays");
        Assert.Contains(new UpdateCustomerCommandValidator().Validate(Update(new() { Id = Guid.NewGuid() }, Guid.NewGuid(), days)).Errors, error => error.PropertyName == "VisitDays");
    }

    [Theory]
    [InlineData(new[] { 1 })]
    [InlineData(new[] { 7 })]
    [InlineData(new[] { 1, 2, 3, 4, 5, 6, 7 })]
    public void One_or_multiple_days_are_accepted(int[] days)
    {
        Assert.True(new CreateCustomerCommandValidator().Validate(Create(days)).IsValid);
        Assert.True(new UpdateCustomerCommandValidator().Validate(Update(new() { Id = Guid.NewGuid() }, Guid.NewGuid(), days)).IsValid);
    }

    [Theory]
    [InlineData("2026-10-05T02:59:59Z", 7)]
    [InlineData("2026-10-05T03:00:00Z", 1)]
    [InlineData("2026-10-06T03:00:00Z", 2)]
    public void Route_day_changes_at_Argentina_midnight(string utc, int expected)
        => Assert.Equal(expected, BusinessTime.IsoDayOfWeek(DateTime.Parse(utc).ToUniversalTime()));

    [Fact]
    public void Legacy_customer_remains_explicitly_unconfigured()
    {
        var dto = CustomerMapping.ToDto(new Customer { CreatedAt = DateTime.UtcNow }, null, new(15, 30), DateTime.UtcNow);
        Assert.Null(dto.VisitDays);
    }

    [Fact]
    public async Task Admin_create_appends_to_zone_with_selected_days()
    {
        Customer? saved = null;
        var customers = Stub<ICustomerRepository>((name, args) => name switch {
            "GetNextRouteOrderAsync" => Task.FromResult(8),
            "AddAsync" => Capture((Customer)args![0]!),
            _ => throw new InvalidOperationException(name),
        });
        Task Capture(Customer customer) { saved = customer; return Task.CompletedTask; }
        var command = Create([5, 1]);
        await new CreateCustomerCommandHandler(customers, Zones(), Work()).Handle(command, default);
        Assert.NotNull(saved);
        Assert.Equal(8, saved.RouteOrder);
        Assert.Equal(command.ZoneId, saved.ZoneId);
        Assert.Equal(new[] { 1, 5 }, saved.VisitDays);
    }

    [Theory]
    [InlineData(false, 4)]
    [InlineData(true, 9)]
    public async Task Editing_days_preserves_order_unless_zone_changes(bool changeZone, int expectedOrder)
    {
        var customer = new Customer { Id = Guid.NewGuid(), ZoneId = Guid.NewGuid(), RouteOrder = 4, VisitDays = [1] };
        var customers = Stub<ICustomerRepository>((name, _) => name switch {
            "GetByIdAsync" => Task.FromResult<Customer?>(customer),
            "GetNextRouteOrderAsync" => Task.FromResult(9),
            "Update" => null,
            _ => throw new InvalidOperationException(name),
        });
        await new UpdateCustomerCommandHandler(customers, Zones(), Work()).Handle(Update(customer, changeZone ? Guid.NewGuid() : customer.ZoneId, [7, 2]), default);
        Assert.Equal(expectedOrder, customer.RouteOrder);
        Assert.Equal(new[] { 2, 7 }, customer.VisitDays);
    }

    private static IZoneRepository Zones() => Stub<IZoneRepository>((_, _) => Task.FromResult(true));
    private static IUnitOfWork Work() => Stub<IUnitOfWork>((_, _) => Task.FromResult(1));
    private static T Stub<T>(Func<string, object?[]?, object?> invoke) where T : class
    {
        var proxy = DispatchProxy.Create<T, TestProxy>();
        ((TestProxy)(object)proxy).Handler = invoke;
        return proxy;
    }
    public class TestProxy : DispatchProxy
    {
        public Func<string, object?[]?, object?> Handler { get; set; } = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!.Name, args);
    }
}
