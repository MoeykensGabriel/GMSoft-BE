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

    private sealed class Fixture
    {
        public int Balance;
        public int Stock = 20;
        public Product Product = new() { Id = Guid.NewGuid(), Detail = "Bidon", SalePrice = 100m, Tracking = ContainerTracking.ByBalance };
        public Customer Customer = new() { Id = Guid.NewGuid(), IsActive = true };
        public DeliverySession Session = new() { Id = Guid.NewGuid() };
        public RegisterDeliveryCommandHandler Handler { get; }

        public Fixture()
        {
            Handler = new(
                Stub<ISessionRepository>((name, _) => name switch
                {
                    "GetOpenByDriverAsync" => Task.FromResult<DeliverySession?>(Session),
                    "GetStockBalanceAsync" => Task.FromResult<IReadOnlyList<SessionStockLineDto>>([new(Product.Id, Product.Detail, Stock, 0)]),
                    "Update" => null,
                    _ => throw new InvalidOperationException(name)
                }),
                Stub<ICustomerRepository>((name, _) => name switch
                {
                    "GetByIdAsync" => Task.FromResult<Customer?>(Customer),
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
