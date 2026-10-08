using System.Reflection;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Customers.RoutePlanning;
using GMSoft.Domain.Entities;

namespace GMSoft.Application.Tests.Features.Customers;

public class RoutePlanningTests
{
    [Fact]
    public async Task Reorder_preserves_positions_and_other_customers()
    {
        var f = new Fixture();
        var response = await f.Save([f.C, f.A, f.B]);
        Assert.Equal(new[] { 2, 6, 10 }, response.Items.Select(c => c.RouteOrder));
        Assert.Equal(new[] { f.C.Id, f.A.Id, f.B.Id }, response.Items.Select(c => c.Id));
        Assert.Equal(4, f.Outside.RouteOrder);
        Assert.True(f.Locked && f.Committed);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Move_first_to_last_or_last_to_first(bool firstToLast)
    {
        var f = new Fixture();
        var order = firstToLast ? new[] { f.B, f.C, f.A } : new[] { f.C, f.A, f.B };
        var result = await f.Save(order);
        Assert.Equal(order.Select(c => c.Id), result.Items.Select(c => c.Id));
    }

    [Fact]
    public async Task Change_days_and_truck_together_preserves_zone_and_position()
    {
        var f = new Fixture();
        var result = await f.Save([f.A, f.B, f.C], [new(f.A.Id, [1, 7], f.OtherTruck)]);
        Assert.Equal(f.OtherTruck, f.A.VehicleId);
        Assert.Equal(new[] { 1, 7 }, f.A.VisitDays);
        Assert.Equal(f.Zone, f.A.ZoneId);
        Assert.Equal(2, f.A.RouteOrder);
        Assert.DoesNotContain(result.Items, c => c.Id == f.A.Id);
        Assert.Equal((await f.Handler.Handle(new GetRoutePlanningQuery(f.Truck, f.Zone, 1), default)).Version,
            result.Version);
    }

    [Fact]
    public async Task Remove_selected_day_returns_updated_membership_and_version()
    {
        var f = new Fixture();
        var version = f.Version;
        var result = await f.Save([f.A, f.B, f.C], [new(f.B.Id, [7])]);
        Assert.Equal(6, f.B.RouteOrder);
        Assert.DoesNotContain(result.Items, c => c.Id == f.B.Id);
        Assert.NotEqual(version, result.Version);
    }

    [Theory]
    [InlineData("new")]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("version")]
    [InlineData("modified")]
    [InlineData("extra")]
    public async Task Stale_list_rejects_every_change(string kind)
    {
        var f = new Fixture();
        var command = f.Command([f.C, f.A, f.B], [new(f.A.Id, [7])]);
        switch (kind)
        {
            case "new": f.All.Add(f.NewCustomer(11)); break;
            case "missing": command = command with { CustomerIds = [f.A.Id, f.B.Id] }; break;
            case "duplicate": command = command with { CustomerIds = [f.A.Id, f.A.Id, f.C.Id] }; break;
            case "version": command = command with { Version = "old" }; break;
            case "modified": f.B.UpdatedAt = f.B.UpdatedAt.AddSeconds(1); break;
            case "extra": command = command with { CustomerIds = [f.A.Id, f.B.Id, f.C.Id, f.Outside.Id] }; break;
        }
        await Assert.ThrowsAsync<ConflictException>(() => f.Handler.Handle(command, default));
        f.AssertUntouched();
    }

    [Theory]
    [InlineData(new int[0])]
    [InlineData(new[] { 1, 1 })]
    [InlineData(new[] { 0 })]
    [InlineData(new[] { 8 })]
    public async Task Invalid_days_reject_whole_batch(int[] days)
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<ValidationException>(() => f.Save([f.C, f.B, f.A],
            [new(f.A.Id, [7]), new(f.B.Id, days)]));
        f.AssertUntouched();
    }

    [Theory]
    [InlineData("truck")]
    [InlineData("zone")]
    [InlineData("customer")]
    [InlineData("batchTruck")]
    public async Task Missing_resource_is_404_and_batch_is_untouched(string kind)
    {
        var f = new Fixture();
        var command = f.Command([f.C, f.B, f.A], [new(f.A.Id, [7])]);
        command = kind switch
        {
            "truck" => command with { VehicleId = Guid.NewGuid() },
            "zone" => command with { ZoneId = Guid.NewGuid() },
            "customer" => command with { CustomerIds = [Guid.NewGuid(), f.B.Id, f.C.Id] },
            _ => command with { Changes = [new(f.A.Id, [7]), new(f.B.Id, [1], Guid.NewGuid())] }
        };
        await Assert.ThrowsAsync<NotFoundException>(() => f.Handler.Handle(command, default));
        f.AssertUntouched();
    }

    [Fact]
    public async Task Query_is_complete_active_filtered_and_ordered()
    {
        var f = new Fixture();
        var inactive = f.NewCustomer(1); inactive.IsActive = false; f.All.Add(inactive);
        var otherDay = f.NewCustomer(3); otherDay.VisitDays = [7]; f.All.Add(otherDay);
        for (var i = 0; i < 120; i++) f.All.Add(f.NewCustomer(20 + i));
        var result = await f.Handler.Handle(new GetRoutePlanningQuery(f.Truck, f.Zone, 1), default);
        Assert.Equal(123, result.Items.Count);
        Assert.Equal(result.Items.OrderBy(c => c.RouteOrder).ThenBy(c => c.Id), result.Items);
    }

    private sealed class Fixture
    {
        public Guid Truck = Guid.NewGuid(), OtherTruck = Guid.NewGuid(), Zone = Guid.NewGuid();
        public Customer A, B, C, Outside;
        public List<Customer> All;
        public RoutePlanningHandler Handler;
        public bool Locked, Committed, Saved;
        public Fixture()
        {
            A = NewCustomer(2); B = NewCustomer(6); C = NewCustomer(10);
            Outside = NewCustomer(4); Outside.VehicleId = OtherTruck;
            All = [A, Outside, B, C];
            var routes = Stub<IRoutePlanningRepository>((name, _) => name switch
            {
                "LockCustomersAsync" => Lock(),
                "GetRouteAsync" => Task.FromResult<IReadOnlyList<Customer>>(Current()),
                _ => throw new InvalidOperationException(name)
            });
            Handler = new(routes,
                Stub<ICustomerRepository>((_, args) => Task.FromResult(All.Any(c => c.Id == (Guid)args![0]!))),
                Stub<IVehicleRepository>((_, args) => Task.FromResult((Guid)args![0]! == Truck || (Guid)args[0]! == OtherTruck)),
                Stub<IZoneRepository>((_, args) => Task.FromResult((Guid)args![0]! == Zone)),
                Stub<IUnitOfWork>((name, args) => name == "ExecuteInTransactionAsync"
                    ? Transaction((Func<Task>)args![0]!) : SaveChanges()));
        }
        private Task Lock() { Locked = true; return Task.CompletedTask; }
        private async Task Transaction(Func<Task> action) { await action(); Committed = true; }
        private Task<int> SaveChanges() { Assert.True(Locked); Saved = true; return Task.FromResult(1); }
        private Customer[] Current() => All.Where(c => c.IsActive && c.VehicleId == Truck &&
            c.ZoneId == Zone && c.VisitDays!.Contains(1)).OrderBy(c => c.RouteOrder).ThenBy(c => c.Id).ToArray();
        public string Version => RoutePlanningHandler.Snapshot(Current(), Truck, Zone, 1).Version;
        public SaveRoutePlanningCommand Command(Customer[] order, RouteCustomerChange[]? changes = null)
            => new(Truck, Zone, 1, Version, order.Select(c => c.Id).ToArray(), changes ?? []);
        public Task<RoutePlanningDto> Save(Customer[] order, RouteCustomerChange[]? changes = null)
            => Handler.Handle(Command(order, changes), default);
        public Customer NewCustomer(int pos) => new() { Id = Guid.NewGuid(), VehicleId = Truck,
            ZoneId = Zone, RouteOrder = pos, VisitDays = [1], UpdatedAt = DateTime.UtcNow,
            ContactName = "Ana", Address = "Calle 123", Phone = "123" };
        public void AssertUntouched()
        {
            Assert.Equal(new[] { 2, 6, 10, 4 }, new[] { A.RouteOrder, B.RouteOrder, C.RouteOrder, Outside.RouteOrder });
            Assert.Equal(new[] { 1 }, A.VisitDays);
            Assert.Equal(Truck, A.VehicleId);
            Assert.False(Saved || Committed);
        }
    }
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
