using System.Reflection;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Customers.GetList;
using GMSoft.Application.Features.Sessions.Common;
using GMSoft.Application.Features.VehicleLoads.Common;
using GMSoft.Application.Features.VehicleLoads.GetPendingSummary;
using GMSoft.Application.Features.VehicleLoads.Register;
using GMSoft.Application.Features.VehicleLoads.UpdateDays;
using GMSoft.Domain.Entities;

namespace GMSoft.Application.Tests.Features.VehicleLoads;

public class RouteDaysTests
{
    private static readonly DateTime MondayUtc = new(2026, 10, 5, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Multiple_batches_resolve_sorted_unique_days()
        => Assert.Equal(new[] { 1, 2 }, RouteDaySelection.Resolve([
            new() { RouteDays = [2, 1] }, new() { RouteDays = [1, 2] }
        ], MondayUtc));

    [Fact]
    public void Legacy_load_defaults_to_opening_day()
        => Assert.Equal(new[] { 1 }, RouteDaySelection.Resolve([new()], MondayUtc));

    [Fact]
    public void Existing_session_keeps_opening_day_instead_of_today()
        => Assert.Equal(new[] { 1 }, SessionMapping.ToDto(new() { OpenedAt = MondayUtc }, []).RouteDays);

    [Fact]
    public void Scheduled_days_override_actual_departure_day()
        => Assert.Equal(new[] { 2, 5 }, SessionMapping.ToDto(new() { OpenedAt = MondayUtc, RouteDays = [2, 5] }, []).RouteDays);

    [Theory]
    [InlineData(new int[] { })]
    [InlineData(new[] { 1, 1 })]
    [InlineData(new[] { 0 })]
    [InlineData(new[] { 8 })]
    public void Invalid_selections_are_rejected_at_all_entry_points(int[] days)
    {
        var load = new RegisterVehicleLoadCommand(Guid.NewGuid(), [new(Guid.NewGuid(), 3)], days);
        Assert.False(new RegisterVehicleLoadCommandValidator().Validate(load).IsValid);
        Assert.False(new UpdateVehicleRouteDaysCommandValidator().Validate(new UpdateVehicleRouteDaysCommand(Guid.NewGuid(), days)).IsValid);
        Assert.False(new GetCustomersQueryValidator().Validate(new GetCustomersQuery(VisitDays: days)).IsValid);
    }

    [Fact]
    public async Task Changing_days_does_not_change_stock_and_updates_all_pending_lines()
    {
        var fixture = new Fixture();
        await fixture.Update.Handle(new(fixture.Vehicle.Id, [5, 2]), default);
        Assert.All(fixture.Pending, line => Assert.Equal(new[] { 2, 5 }, line.RouteDays));
        Assert.Equal(new[] { 10, 20 }, fixture.Pending.Select(line => line.Quantity));
        Assert.Empty(fixture.Added);
        Assert.Equal(1, fixture.Saves);
    }

    [Fact]
    public async Task Adding_stock_copies_days_to_entire_pending_load()
    {
        var fixture = new Fixture();
        await fixture.Register.Handle(new(fixture.Vehicle.Id, [new(Guid.NewGuid(), 7)], [3, 1]), default);
        Assert.All(fixture.Pending.Concat(fixture.Added), line => Assert.Equal(new[] { 1, 3 }, line.RouteDays));
        Assert.Equal(7, Assert.Single(fixture.Added).Quantity);
        Assert.Equal(1, fixture.Saves);
    }

    [Fact]
    public async Task Retrying_the_same_batch_does_not_load_it_twice()
    {
        var fixture = new Fixture();
        var batch = new RegisterVehicleLoadCommand(fixture.Vehicle.Id, [new(Guid.NewGuid(), 5)], [1], Guid.NewGuid());
        var first = await fixture.Register.Handle(batch, default);
        var retry = await fixture.Register.Handle(batch, default);
        Assert.Equal(first.LoadedAt, retry.LoadedAt);
        Assert.Equal(5, Assert.Single(fixture.Added).Quantity);
        Assert.Equal(1, fixture.Saves);
    }

    [Fact]
    public async Task Driver_summary_adds_batches_of_the_same_product_by_id()
    {
        var bidon = new Product { Id = Guid.NewGuid(), Detail = "Bidón de 20 litros" };
        // Mismo nombre, otro producto: no se mezcla.
        var otro = new Product { Id = Guid.NewGuid(), Detail = "Bidón de 20 litros" };
        IReadOnlyList<VehicleLoad> pending =
        [
            new() { ProductId = bidon.Id, Product = bidon, Quantity = 5, RouteDays = [2, 1] },
            new() { ProductId = bidon.Id, Product = bidon, Quantity = 2, RouteDays = [1, 2] },
            new() { ProductId = otro.Id, Product = otro, Quantity = 4, RouteDays = [1, 2] },
        ];
        var handler = new GetPendingVehicleLoadSummaryQueryHandler(
            Stub<IVehicleLoadRepository>((_, _) => Task.FromResult(pending)));
        var summary = await handler.Handle(new(Guid.NewGuid()), default);
        Assert.Equal(new[] { 1, 2 }, summary.RouteDays);
        Assert.Equal(2, summary.Lines.Count);
        Assert.Equal(7, summary.Lines.Single(line => line.ProductId == bidon.Id).Quantity);
        Assert.Equal(4, summary.Lines.Single(line => line.ProductId == otro.Id).Quantity);
    }

    [Fact]
    public async Task Older_load_client_does_not_reset_existing_schedule()
    {
        var fixture = new Fixture();
        await fixture.Register.Handle(new(fixture.Vehicle.Id, [new(Guid.NewGuid(), 7)]), default);
        Assert.Equal(new[] { 1, 2 }, Assert.Single(fixture.Added).RouteDays);
    }

    [Fact]
    public async Task Driver_cannot_change_days()
    {
        var fixture = new Fixture { Admin = false };
        await Assert.ThrowsAsync<ForbiddenException>(() => fixture.Update.Handle(new(fixture.Vehicle.Id, [1]), default));
        Assert.Equal(0, fixture.Saves);
    }

    [Fact]
    public async Task Open_departure_cannot_be_rescheduled()
    {
        var fixture = new Fixture { OnRoute = true };
        await Assert.ThrowsAsync<ConflictException>(() => fixture.Update.Handle(new(fixture.Vehicle.Id, [1]), default));
        Assert.Equal(0, fixture.Saves);
    }

    [Fact]
    public async Task Days_only_update_requires_a_pending_load()
    {
        var fixture = new Fixture();
        fixture.Pending.Clear();
        await Assert.ThrowsAsync<ConflictException>(() => fixture.Update.Handle(new(fixture.Vehicle.Id, [1]), default));
        Assert.Equal(0, fixture.Saves);
    }

    private sealed class Fixture
    {
        public bool Admin = true;
        public bool OnRoute;
        public int Saves;
        public Vehicle Vehicle = new() { Id = Guid.NewGuid() };
        public List<VehicleLoad> Pending = [new() { Quantity = 10, RouteDays = [1, 2] }, new() { Quantity = 20, RouteDays = [1, 2] }];
        public List<VehicleLoad> Added = [];
        public UpdateVehicleRouteDaysCommandHandler Update { get; }
        public RegisterVehicleLoadCommandHandler Register { get; }
        public Fixture()
        {
            var loads = Stub<IVehicleLoadRepository>((name, args) => name switch {
                "GetPendingAsync" => Task.FromResult<IReadOnlyList<VehicleLoad>>(Pending),
                "Update" => null,
                "AddAsync" => Add((VehicleLoad)args![0]!),
                "GetLoadedAtByClientRequestAsync" => Task.FromResult(Added
                    .Where(line => line.ClientRequestId == (Guid)args![0]!)
                    .Select(line => (DateTime?)line.LoadedAt).FirstOrDefault()),
                _ => throw new InvalidOperationException(name)
            });
            var vehicles = Stub<IVehicleRepository>((_, _) => Task.FromResult<Vehicle?>(Vehicle));
            var sessions = Stub<ISessionRepository>((_, _) => Task.FromResult(OnRoute));
            var user = Stub<ICurrentUserService>((name, _) => name == "IsInRole" ? Admin : null);
            var work = Stub<IUnitOfWork>((_, _) => Task.FromResult(++Saves));
            Update = new(loads, vehicles, sessions, user, work);
            Register = new(loads, vehicles, sessions, Stub<IRepository<Product>>((_, _) => Task.FromResult(true)), user, work);
        }
        private Task Add(VehicleLoad line) { Added.Add(line); return Task.CompletedTask; }
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
