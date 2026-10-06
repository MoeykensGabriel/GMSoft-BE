using System.Reflection;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Deliveries.Register;
using GMSoft.Application.Features.Sessions.Common;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;

namespace GMSoft.Application.Tests.Features.Deliveries;

public class RegisterDeliveryWorkflowTests
{
    [Fact]
    public async Task Successful_sale_clears_only_its_pending_visit()
    {
        var fixture = new Fixture();
        var other = Guid.NewGuid();
        fixture.Session.DeferredCustomerIds = [fixture.Customer.Id, other];
        await fixture.Handler.Handle(fixture.Request(1, 0), default);
        Assert.Equal(new[] { other }, fixture.Session.DeferredCustomerIds);
    }

    [Fact]
    public async Task Rejected_sale_keeps_pending_visit()
    {
        var fixture = new Fixture { Stock = 0 };
        fixture.Session.DeferredCustomerIds = [fixture.Customer.Id];
        await Assert.ThrowsAsync<ConflictException>(() => fixture.Handler.Handle(fixture.Request(1, 0), default));
        Assert.Equal(new[] { fixture.Customer.Id }, fixture.Session.DeferredCustomerIds);
    }

    [Fact]
    public async Task Promotion_leaves_containers_and_uses_stock_without_money_debt()
    {
        var fixture = new Fixture();
        var result = await fixture.Handler.Handle(fixture.Request(3, 0) with { Type = DeliveryType.Promotion }, default);
        Assert.Equal(0m, result.Total);
        Assert.Equal(0m, result.SaldoCuentaCliente);
        Assert.Equal(3, fixture.Balance);
        var delivery = Assert.Single(fixture.Session.Deliveries);
        Assert.Equal(DeliveryType.Promotion, delivery.Type);
        Assert.Equal(0m, Assert.Single(delivery.Items).UnitPrice);
        Assert.Equal(-3, Assert.Single(fixture.Session.StockMovements).Quantity);
    }

    [Fact]
    public async Task Promotion_requires_available_full_stock()
    {
        var fixture = new Fixture { Stock = 2 };
        await Assert.ThrowsAsync<ConflictException>(() => fixture.Handler.Handle(
            fixture.Request(3, 0) with { Type = DeliveryType.Promotion }, default));
        Assert.Empty(fixture.Session.Deliveries);
    }

    [Fact]
    public async Task Promotion_cannot_collect_more_containers_than_customer_has()
    {
        var fixture = new Fixture();
        await Assert.ThrowsAsync<ConflictException>(() => fixture.Handler.Handle(
            fixture.Request(3, 4) with { Type = DeliveryType.Promotion }, default));
        Assert.Empty(fixture.Session.Deliveries);
    }

    [Theory]
    [InlineData(0, 3, 0, 3)]
    [InlineData(3, 4, 3, 4)]
    [InlineData(4, 4, 4, 4)]
    [InlineData(3, 0, 3, 0)]
    public async Task Sale_and_returns_update_real_container_balance(int previous, int sold, int returned, int expected)
    {
        var fixture = new Fixture { Balance = previous };
        var result = await fixture.Handler.Handle(fixture.Request(sold, returned), default);
        Assert.Equal(expected, fixture.Balance);
        Assert.Equal(sold * 100m, result.Total);
        var delivery = Assert.Single(fixture.Session.Deliveries);
        Assert.Equal(sold - returned, delivery.ContainerMovements.Sum(m => m.Quantity));
        Assert.Equal(-sold, fixture.Session.StockMovements.Where(m => m.State == ContainerState.Full).Sum(m => m.Quantity));
        Assert.Equal(returned, fixture.Session.StockMovements.Where(m => m.State == ContainerState.Empty).Sum(m => m.Quantity));
    }

    [Fact]
    public async Task Cannot_return_more_than_previous_balance_plus_delivery()
    {
        var fixture = new Fixture { Balance = 3 };
        await Assert.ThrowsAsync<ConflictException>(() => fixture.Handler.Handle(fixture.Request(4, 8), default));
        Assert.Empty(fixture.Session.Deliveries);
        Assert.Empty(fixture.Session.StockMovements);
        Assert.Equal(3, fixture.Balance);
    }

    [Fact]
    public async Task Cannot_sell_more_than_full_stock()
    {
        var fixture = new Fixture { Stock = 2 };
        await Assert.ThrowsAsync<ConflictException>(() => fixture.Handler.Handle(fixture.Request(3, 0), default));
        Assert.Empty(fixture.Session.Deliveries);
    }

    [Fact]
    public async Task Explicit_container_count_is_not_double_counted()
    {
        var fixture = new Fixture();
        var request = fixture.Request(3, 0) with { ContainersOut = [new(fixture.Product.Id, 3)] };
        await fixture.Handler.Handle(request, default);
        Assert.Equal(3, fixture.Balance);
    }

    [Fact]
    public async Task Explicit_container_count_cannot_override_sale()
    {
        var fixture = new Fixture();
        var request = fixture.Request(3, 0) with { ContainersOut = [new(fixture.Product.Id, 2)] };
        await Assert.ThrowsAsync<BadRequestException>(() => fixture.Handler.Handle(request, default));
        Assert.Empty(fixture.Session.Deliveries);
    }

    [Fact]
    public async Task Non_returnable_product_does_not_create_container_debt()
    {
        var fixture = new Fixture();
        fixture.Product.Tracking = ContainerTracking.None;
        await fixture.Handler.Handle(fixture.Request(3, 0), default);
        Assert.Equal(0, fixture.Balance);
        Assert.Empty(Assert.Single(fixture.Session.Deliveries).ContainerMovements);
    }

    [Theory]
    [InlineData(DeliveryType.Sale)]
    [InlineData(DeliveryType.Promotion)]
    [InlineData(DeliveryType.ContainerOnly)]
    public async Task Completed_visit_updates_timestamp_for_all_delivery_types(DeliveryType type)
    {
        var fixture = new Fixture { Balance = 2 };
        var old = DateTime.UtcNow.AddDays(-2);
        fixture.Customer.LastVisitAt = old;
        var request = type == DeliveryType.ContainerOnly ? fixture.Request(0, 1) : fixture.Request(1, 0) with { Type = type };
        await fixture.Handler.Handle(request, default);
        Assert.Equal(Assert.Single(fixture.Session.Deliveries).DeliveredAt, fixture.Customer.LastVisitAt);
        Assert.True(fixture.Customer.LastVisitAt > old);
    }

    [Fact]
    public async Task Rejected_visit_preserves_last_visit()
    {
        var fixture = new Fixture();
        var old = DateTime.UtcNow.AddDays(-2);
        fixture.Customer.LastVisitAt = old;
        await Assert.ThrowsAsync<ConflictException>(() => fixture.Handler.Handle(fixture.Request(1, 3), default));
        Assert.Equal(old, fixture.Customer.LastVisitAt);
    }

    [Theory]
    [InlineData("vehicle")]
    [InlineData("zone")]
    [InlineData("day")]
    public async Task Direct_sale_outside_route_is_rejected_without_movements(string mismatch)
    {
        var fixture = new Fixture();
        if (mismatch == "vehicle") fixture.Customer.VehicleId = Guid.NewGuid();
        if (mismatch == "zone") fixture.Customer.ZoneId = Guid.NewGuid();
        if (mismatch == "day") fixture.Customer.VisitDays = [7];
        await Assert.ThrowsAsync<BadRequestException>(() => fixture.Handler.Handle(fixture.Request(1, 0), default));
        Assert.Empty(fixture.Session.Deliveries);
        Assert.Empty(fixture.Session.StockMovements);
        Assert.Null(fixture.Customer.LastVisitAt);
    }

    [Theory]
    [InlineData(DeliveryType.Sale)]
    [InlineData(DeliveryType.Promotion)]
    public async Task Street_creation_uses_departure_vehicle_and_zone_and_selected_future_days(DeliveryType type)
    {
        var fixture = new Fixture();
        var request = fixture.Request(1, 0) with {
            CustomerId = null, Type = type,
            NewCustomer = new(null, "Ana", "123", "Calle 1", null, [7, 5])
        };
        await fixture.Handler.Handle(request, default);
        Assert.Equal(fixture.Session.VehicleId, fixture.Customer.VehicleId);
        Assert.Equal(fixture.Session.ZoneId, fixture.Customer.ZoneId);
        Assert.Equal(new[] { 5, 7 }, fixture.Customer.VisitDays);
        Assert.Equal(8, fixture.Customer.RouteOrder);
        Assert.Equal(Assert.Single(fixture.Session.Deliveries).DeliveredAt, fixture.Customer.LastVisitAt);
    }

    private sealed class Fixture
    {
        public int Balance;
        public int Stock = 20;
        public Product Product = new() { Id = Guid.NewGuid(), Detail = "Bidon", SalePrice = 100m, Tracking = ContainerTracking.ByBalance };
        public Customer Customer = new() { Id = Guid.NewGuid(), IsActive = true };
        public DeliverySession Session = new() { Id = Guid.NewGuid(), VehicleId = Guid.NewGuid(), ZoneId = Guid.NewGuid(), RouteDays = [1] };
        public RegisterDeliveryCommandHandler Handler { get; }

        public Fixture()
        {
            Customer.VehicleId = Session.VehicleId;
            Customer.ZoneId = Session.ZoneId;
            Customer.VisitDays = [1];
            Handler = new(
                Stub<ISessionRepository>((name, _) => name switch
                {
                    "GetOpenByDriverAsync" => Task.FromResult<DeliverySession?>(Session),
                    "GetStockBalanceAsync" => Task.FromResult<IReadOnlyList<SessionStockLineDto>>([new(Product.Id, Product.Detail, Stock, 0)]),
                    "Update" => null,
                    _ => throw new InvalidOperationException(name)
                }),
                Stub<ICustomerRepository>((name, args) => name switch
                {
                    "GetByIdAsync" => Task.FromResult<Customer?>(Customer),
                    "GetNextRouteOrderAsync" => Task.FromResult(8),
                    "AddAsync" => AddCustomer((Customer)args![0]!),
                    "Update" => null,
                    "GetAccountBalanceAsync" => Task.FromResult(Session.Deliveries.Sum(d => d.Total)),
                    _ => throw new InvalidOperationException(name)
                }),
                Stub<IProductRepository>((_, _) => Task.FromResult<Product?>(Product)),
                Stub<ICustomerPriceRepository>((_, _) => Task.FromResult<decimal?>(null)),
                Stub<IContainerBalanceRepository>((name, args) => name switch
                {
                    "GetAsync" => Task.FromResult<CustomerContainerBalance?>(new() { Quantity = Balance }),
                    "AdjustAsync" => Adjust((int)args![2]!),
                    _ => throw new InvalidOperationException(name)
                }),
                Stub<IRepository<Payment>>((name, _) => throw new InvalidOperationException(name)),
                Stub<ICurrentUserService>((_, _) => (Guid?)Guid.NewGuid()),
                Stub<IUnitOfWork>((name, args) => name switch
                {
                    "ExecuteInTransactionAsync" => ((Func<Task>)args![0]!)(),
                    "SaveChangesAsync" => Task.FromResult(1),
                    _ => throw new InvalidOperationException(name)
                }));
        }

        private Task AddCustomer(Customer customer) { Customer = customer; Customer.Id = Guid.NewGuid(); return Task.CompletedTask; }
        private Task Adjust(int delta) { Balance += delta; return Task.CompletedTask; }

        public RegisterDeliveryCommand Request(int sold, int returned) => new(
            Customer.Id, null, sold > 0 ? DeliveryType.Sale : DeliveryType.ContainerOnly,
            sold > 0 ? [new(Product.Id, sold)] : [], [],
            returned > 0 ? [new(Product.Id, returned)] : [], null, null);
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
