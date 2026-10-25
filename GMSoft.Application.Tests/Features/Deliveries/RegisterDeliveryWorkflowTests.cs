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
    public async Task Collected_sale_without_amount_charges_the_server_total()
    {
        var fixture = new Fixture();
        var request = fixture.Request(3, 0) with { Payment = new PaymentLine(null, PaymentMethod.Transfer) };
        var result = await fixture.Handler.Handle(request, default);
        var payment = Assert.Single(fixture.Payments);
        Assert.Equal(300m, result.Total);
        Assert.Equal(300m, payment.Amount);
        Assert.Equal(PaymentMethod.Transfer, payment.Method);
        Assert.Equal(fixture.Session.Id, payment.DeliverySessionId);
    }

    [Fact]
    public async Task Sale_left_as_debt_registers_no_payment()
    {
        var fixture = new Fixture();
        await fixture.Handler.Handle(fixture.Request(3, 0), default);
        Assert.Empty(fixture.Payments);
    }

    [Fact]
    public async Task Retrying_the_same_visit_does_not_duplicate_sale_stock_or_payment()
    {
        var fixture = new Fixture();
        var request = fixture.Request(2, 0) with
        {
            Payment = new PaymentLine(null, PaymentMethod.Cash), ClientRequestId = Guid.NewGuid()
        };
        var first = await fixture.Handler.Handle(request, default);
        var retry = await fixture.Handler.Handle(request, default);
        Assert.Equal(first, retry);
        Assert.Single(fixture.Session.Deliveries);
        Assert.Single(fixture.Payments);
        Assert.Equal(2, fixture.Balance);
        Assert.Equal(-2, fixture.Session.StockMovements.Sum(m => m.Quantity));
    }

    [Fact]
    public void Charging_the_total_only_applies_to_a_sale()
    {
        var fixture = new Fixture();
        var visit = fixture.Request(0, 1) with { Payment = new PaymentLine(null, PaymentMethod.Cash) };
        Assert.False(new RegisterDeliveryCommandValidator().Validate(visit).IsValid);
        Assert.True(new RegisterDeliveryCommandValidator().Validate(
            fixture.Request(1, 0) with { Payment = new PaymentLine(null, PaymentMethod.Cash) }).IsValid);
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
    public async Task Returning_more_than_previous_balance_counts_the_rest_as_unregistered()
    {
        // Tenía 8 (3 registrados y 5 sin registrar), devuelve los 8 y se lleva 4 llenos:
        // se queda con 4. Los llenos de esta visita no se descuentan de lo que devuelve.
        var fixture = new Fixture { Balance = 3 };
        await fixture.Handler.Handle(fixture.Request(4, 8), default);
        Assert.Equal(4, fixture.Balance);
        Assert.Equal(8, fixture.Session.StockMovements.Where(m => m.State == ContainerState.Empty).Sum(m => m.Quantity));
        Assert.Contains(Assert.Single(fixture.Session.Deliveries).ContainerMovements, m => m.Notes == "Tenía sin registrar");
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
        fixture.Stock = 0;
        await Assert.ThrowsAsync<ConflictException>(() => fixture.Handler.Handle(fixture.Request(1, 0), default));
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

    private static RegisterDeliveryCommand NoPurchase(Fixture f) => f.Request(0, 0) with
    { Type = DeliveryType.NoPurchase, NoPurchaseReason = NoPurchaseReason.NotHome, ClientRequestId = Guid.NewGuid() };

    [Fact]
    public async Task No_purchase_updates_visit_without_stock_containers_money()
    {
        var f = new Fixture { Balance = 4 };
        var request = NoPurchase(f);
        var result = await f.Handler.Handle(request, default);
        Assert.Equal(0, result.Total); Assert.Equal(0, result.SaldoCuentaCliente);
        Assert.Empty(f.Payments); Assert.Empty(f.Session.StockMovements); Assert.Equal(4, f.Balance);
        var visit = Assert.Single(f.Session.Deliveries);
        Assert.Equal(NoPurchaseReason.NotHome, visit.NoPurchaseReason);
        Assert.Equal(visit.DeliveredAt, f.Customer.LastVisitAt);
        Assert.Equal(result, await f.Handler.Handle(request, default));
        Assert.Single(f.Session.Deliveries);
    }

    [Fact]
    public async Task Correction_keeps_one_visit_and_old_retry_does_not_revert_reason()
    {
        var f = new Fixture(); var first = NoPurchase(f);
        var result = await f.Handler.Handle(first, default);
        await f.Handler.Handle(first with { ClientRequestId = Guid.NewGuid(), NoPurchaseReason = NoPurchaseReason.Other, Notes = " Volver maÃ±ana " }, default);
        Assert.Equal(result, await f.Handler.Handle(first, default));
        var visit = Assert.Single(f.Session.Deliveries);
        Assert.Equal(NoPurchaseReason.Other, visit.NoPurchaseReason); Assert.Equal("Volver maÃ±ana", visit.Notes);
    }

    [Fact]
    public async Task Sale_after_no_purchase_is_allowed_but_new_no_purchase_after_sale_conflicts()
    {
        var f = new Fixture(); await f.Handler.Handle(NoPurchase(f), default);
        await f.Handler.Handle(f.Request(1, 0), default);
        await Assert.ThrowsAsync<ConflictException>(() => f.Handler.Handle(NoPurchase(f), default));
        Assert.Equal(2, f.Session.Deliveries.Count);
    }

    [Theory]
    [InlineData("vehicle")][InlineData("zone")][InlineData("day")]
    public async Task No_purchase_outside_route_is_rejected(string mismatch)
    {
        var f = new Fixture();
        if (mismatch == "vehicle") f.Customer.VehicleId = Guid.NewGuid();
        if (mismatch == "zone") f.Customer.ZoneId = Guid.NewGuid();
        if (mismatch == "day") f.Customer.VisitDays = [7];
        await Assert.ThrowsAsync<BadRequestException>(() => f.Handler.Handle(NoPurchase(f), default));
        Assert.Empty(f.Session.Deliveries);
    }

    [Theory]
    [InlineData("reason")][InlineData("unknown")][InlineData("other")][InlineData("long")]
    [InlineData("items")][InlineData("out")][InlineData("in")][InlineData("payment")][InlineData("key")]
    public void No_purchase_validations(string invalid)
    {
        var f = new Fixture(); var valid = NoPurchase(f);
        Assert.True(new RegisterDeliveryCommandValidator().Validate(valid).IsValid);
        var request = invalid switch
        {
            "reason" => valid with { NoPurchaseReason = null },
            "unknown" => valid with { NoPurchaseReason = (NoPurchaseReason)99 },
            "other" => valid with { NoPurchaseReason = NoPurchaseReason.Other, Notes = "  " },
            "long" => valid with { Notes = new string('x', 301) },
            "items" => valid with { Items = [new(f.Product.Id, 1)] },
            "out" => valid with { ContainersOut = [new(f.Product.Id, 1)] },
            "in" => valid with { ContainersIn = [new(f.Product.Id, 1)] },
            "payment" => valid with { Payment = new(1, PaymentMethod.Cash) },
            _ => valid with { ClientRequestId = null }
        };
        Assert.False(new RegisterDeliveryCommandValidator().Validate(request).IsValid);
    }

    private sealed class Fixture
    {
        public int Balance;
        public int Stock = 20;
        public Product Product = new() { Id = Guid.NewGuid(), Detail = "Bidon", SalePrice = 100m, Tracking = ContainerTracking.ByBalance };
        public Customer Customer = new() { Id = Guid.NewGuid(), IsActive = true };
        public DeliverySession Session = new() { Id = Guid.NewGuid(), VehicleId = Guid.NewGuid(), ZoneId = Guid.NewGuid(), RouteDays = [1] };
        public RegisterDeliveryCommandHandler Handler { get; }

        private object? SetLastVisit(DateTime at) { Customer.LastVisitAt = at; return null; }

        public Fixture()
        {
            Customer.VehicleId = Session.VehicleId;
            Customer.ZoneId = Session.ZoneId;
            Customer.VisitDays = [1];
            Handler = new(
                Stub<ISessionRepository>((name, args) => name switch
                {
                    "GetCustomerVisitsAsync" => Task.FromResult<IReadOnlyList<Delivery>>(Session.Deliveries.ToList()),
                    "GetByIdAsync" or "GetOpenByDriverAsync" => Task.FromResult<DeliverySession?>(Session),
                    "LockOpenAsync" => Task.FromResult(Session.Status == SessionStatus.Open),
                    "GetStockBalanceAsync" => Task.FromResult<IReadOnlyList<SessionStockLineDto>>([new(Product.Id, Product.Detail, Stock, 0)]),
                    "GetDeliveryByClientRequestAsync" => Task.FromResult(
                        Session.Deliveries.FirstOrDefault(d => d.ClientRequestId == (Guid)args![0]! || (d.NoPurchaseRequestIds ?? []).Contains((Guid)args![0]!))),
                    "Update" => null,
                    _ => throw new InvalidOperationException(name)
                }),
                Stub<ICustomerRepository>((name, args) => name switch
                {
                    "GetByIdAsync" => Task.FromResult<Customer?>(Customer),
                    "GetNextRouteOrderAsync" => Task.FromResult(8),
                    "AddAsync" => AddCustomer((Customer)args![0]!),
                    "UpdateLastVisit" => SetLastVisit((DateTime)args![1]!),
                    "Update" => null,
                    "GetAccountBalanceAsync" => Task.FromResult(Session.Deliveries.Sum(d => d.Total)),
                    _ => throw new InvalidOperationException(name)
                }),
                Stub<IProductRepository>((_, _) => Task.FromResult<Product?>(Product)),
                Stub<ICustomerPriceRepository>((_, _) => Task.FromResult<decimal?>(null)),
                Stub<IContainerBalanceRepository>((name, args) => name switch
                {
                    "GetAsync" => Task.FromResult<CustomerContainerBalance?>(new() { Quantity = Balance }),
                    "LockQuantityAsync" => Task.FromResult(Balance),
                    "AdjustAsync" => Adjust((int)args![2]!),
                    _ => throw new InvalidOperationException(name)
                }),
                Stub<IRepository<Payment>>((name, args) => name == "AddAsync"
                    ? AddPayment((Payment)args![0]!)
                    : throw new InvalidOperationException(name)),
                Stub<ICurrentUserService>((_, _) => (Guid?)Session.DriverId),
                Stub<IUnitOfWork>((name, args) => name switch
                {
                    "ExecuteInTransactionAsync" => ((Func<Task>)args![0]!)(),
                    "SaveChangesAsync" => Task.FromResult(1),
                    _ => throw new InvalidOperationException(name)
                }), WorkingHoursTestSupport.Unrestricted());
        }

        public List<Payment> Payments = [];
        private Task AddPayment(Payment payment) { Payments.Add(payment); return Task.CompletedTask; }
        private Task AddCustomer(Customer customer) { Customer = customer; Customer.Id = Guid.NewGuid(); return Task.CompletedTask; }
        private Task Adjust(int delta) { Balance += delta; return Task.CompletedTask; }

        public RegisterDeliveryCommand Request(int sold, int returned) => new(
            Customer.Id, null, sold > 0 ? DeliveryType.Sale : DeliveryType.ContainerOnly,
            sold > 0 ? [new(Product.Id, sold)] : [], [],
            returned > 0 ? [new(Product.Id, returned)] : [], null, null, Guid.NewGuid());
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
