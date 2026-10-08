using System.Reflection;
using FluentValidation;
using GMSoft.Application.Common;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Deliveries.Register;
using GMSoft.Application.Features.Promotions.Close;
using GMSoft.Application.Features.Promotions.Common;
using GMSoft.Application.Features.Promotions.GetSettings;
using GMSoft.Application.Features.Promotions.List;
using GMSoft.Application.Features.Promotions.Pending;
using GMSoft.Application.Features.Promotions.Register;
using GMSoft.Application.Features.Promotions.UpdateSettings;
using GMSoft.Application.Features.Sessions.Common;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;

namespace GMSoft.Application.Tests.Features.Promotions;

public class PromotionWorkflowTests
{
    [Fact]
    public async Task Registration_consumes_full_stock_and_loans_without_creating_customer_or_delivery()
    {
        var f = new Fixture();
        var result = await f.Register.Handle(f.Request(), default);
        var p = Assert.Single(f.Promotions);
        Assert.Equal(p.Id, result.PromotionId);
        Assert.Equal(f.Session.VehicleId, p.VehicleId);
        Assert.Equal(f.Session.ZoneId, p.ZoneId);
        Assert.Equal(f.Session.Id, p.DeliverySessionId);
        Assert.Equal(f.DriverId, p.DriverId);
        Assert.Equal(new[] { 2, 5 }, p.VisitDays);
        Assert.Equal("Ana", p.ContactName);
        var line = Assert.Single(p.Lines);
        Assert.Equal(3, line.Quantity);
        Assert.Equal(3, line.ContainersLoaned);
        var movement = Assert.Single(p.ContainerMovements).ContainerMovement;
        Assert.Equal(3, movement.Quantity);
        Assert.Null(movement.CustomerId);
        Assert.Equal(ContainerMovementType.DeliveredToCustomer, movement.Type);
        Assert.Equal(-3, Assert.Single(f.Session.StockMovements).Quantity);
        Assert.Empty(f.Customers);
        Assert.Empty(f.Session.Deliveries);
        Assert.Equal(0, f.Balance);
    }

    [Fact]
    public async Task Insufficient_stock_is_rejected_without_effects()
    {
        var f = new Fixture { Stock = 2 };
        await Assert.ThrowsAsync<ConflictException>(() => f.Register.Handle(f.Request(), default));
        Assert.Empty(f.Promotions);
        Assert.Empty(f.Session.StockMovements);
    }

    [Fact]
    public async Task ByUnit_is_rejected_without_effects()
    {
        var f = new Fixture();
        f.Product.Tracking = ContainerTracking.ByUnit;
        await Assert.ThrowsAsync<BadRequestException>(() => f.Register.Handle(f.Request(), default));
        Assert.Empty(f.Promotions);
        Assert.Empty(f.Session.StockMovements);
    }

    [Fact]
    public async Task None_consumes_stock_but_does_not_loan_containers()
    {
        var f = new Fixture();
        f.Product.Tracking = ContainerTracking.None;
        await f.Register.Handle(f.Request(), default);
        Assert.Equal(0, Assert.Single(Assert.Single(f.Promotions).Lines).ContainersLoaned);
        Assert.Empty(f.Promotions[0].ContainerMovements);
        Assert.Equal(-3, Assert.Single(f.Session.StockMovements).Quantity);
    }

    [Fact]
    public async Task Registration_retry_does_not_repeat_effects_even_after_departure_closed()
    {
        var f = new Fixture();
        var r = f.Request();
        var first = await f.Register.Handle(r, default);
        f.Open = false;
        Assert.Equal(first, await f.Register.Handle(r, default));
        Assert.Single(f.Promotions);
        Assert.Single(f.Session.StockMovements);
        Assert.Single(f.Promotions[0].ContainerMovements);
    }

    [Fact]
    public async Task Request_key_cannot_disclose_another_drivers_promotion()
    {
        var f = new Fixture();
        var r = f.Request();
        await f.Register.Handle(r, default);
        f.DriverId = Guid.NewGuid();
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Register.Handle(r, default));
    }

    [Fact]
    public async Task Registration_requires_open_departure()
    {
        var f = new Fixture { Open = false };
        await Assert.ThrowsAsync<ConflictException>(() => f.Register.Handle(f.Request(), default));
        Assert.Empty(f.Promotions);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(12)]
    public async Task Pickup_uses_configured_days_and_business_date_and_is_frozen(int days)
    {
        var f = new Fixture { PickupDays = days };
        var result = await f.Register.Handle(f.Request(), default);
        Assert.Equal(DateOnly.FromDateTime(result.RegisteredAt.Add(BusinessTime.Offset)).AddDays(days), result.PickupDate);
        f.PickupDays = 30;
        Assert.Equal(result.PickupDate, f.Promotions[0].PickupDate);
    }

    [Fact]
    public async Task Conversion_transfers_containers_and_preserves_original_route_without_sale()
    {
        var f = new Fixture();
        var registered = await f.Register.Handle(f.Request(), default);
        var originalZone = f.Session.ZoneId;
        f.Session.ZoneId = Guid.NewGuid(); // Hoy el camion reparte otra zona.
        var fullMovements = f.Session.StockMovements.Count;
        var result = await f.Close.Handle(f.CloseRequest(true), default);
        var customer = Assert.Single(f.Customers);
        Assert.Equal(customer.Id, result.CustomerId);
        Assert.Equal("Converted", result.Status);
        Assert.Equal("Ana", customer.ContactName);
        Assert.Equal(originalZone, customer.ZoneId);
        Assert.Equal(f.Session.VehicleId, customer.VehicleId);
        Assert.Equal(new[] { 2, 5 }, customer.VisitDays);
        Assert.Equal(8, customer.RouteOrder);
        Assert.Null(customer.LastVisitAt);
        Assert.Equal(3, f.Balance);
        Assert.Empty(f.Session.Deliveries);
        Assert.Equal(fullMovements, f.Session.StockMovements.Count);
        var p = Assert.Single(f.Promotions);
        Assert.Equal(registered.PromotionId, p.Id);
        Assert.Equal(0, p.ContainerMovements.Where(m => m.ContainerMovement.CustomerId is null).Sum(m => m.ContainerMovement.Quantity));
        var transferred = Assert.Single(p.ContainerMovements, m => m.ContainerMovement.CustomerId == customer.Id);
        Assert.Equal(3, transferred.ContainerMovement.Quantity);
        Assert.DoesNotContain(p.ContainerMovements, m => m.ContainerMovement.Type == ContainerMovementType.ReturnedFromCustomer);
    }

    [Fact]
    public async Task Conversion_can_replace_prospect_data_with_valid_complete_customer_data()
    {
        var f = new Fixture();
        await f.Register.Handle(f.Request(), default);
        await f.Close.Handle(f.CloseRequest(true) with
        {
            Customer = new("Comercio", "Otra persona", "321", "Calle 2", "Horario", [7])
        }, default);
        var c = Assert.Single(f.Customers);
        Assert.Equal("Otra persona", c.ContactName);
        Assert.Equal(new[] { 7 }, c.VisitDays);
        Assert.Equal("Ana", f.Promotions[0].ContactName); // Conserva el prospecto original.
    }

    [Theory]
    [InlineData(3, 0)]
    [InlineData(1, 2)]
    [InlineData(0, 3)]
    public async Task No_conversion_collects_empties_and_records_every_missing_container_as_loss(int returned, int lost)
    {
        var f = new Fixture();
        await f.Register.Handle(f.Request(), default);
        var result = await f.Close.Handle(f.CloseRequest(false, returned), default);
        Assert.Equal("NotConverted", result.Status);
        Assert.Null(result.CustomerId);
        Assert.Empty(f.Customers);
        Assert.Equal(0, f.Balance);
        var p = Assert.Single(f.Promotions);
        var line = Assert.Single(p.Lines);
        Assert.Equal(returned, line.ContainersReturned);
        Assert.Equal(lost, line.ContainersLost);
        Assert.Equal(returned, f.Session.StockMovements.Where(m => m.State == ContainerState.Empty).Sum(m => m.Quantity));
        Assert.Equal(-lost, p.ContainerMovements.Where(m => m.ContainerMovement.Type == ContainerMovementType.Lost)
            .Sum(m => m.ContainerMovement.Quantity));
        Assert.Equal(0, p.ContainerMovements.Sum(m => m.ContainerMovement.Quantity));
        Assert.Equal(f.Session.Id, p.ClosingSessionId);
        Assert.Equal(f.DriverId, p.ClosedByDriverId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Closure_retry_is_idempotent_after_departure_closed(bool convert)
    {
        var f = new Fixture();
        await f.Register.Handle(f.Request(), default);
        var request = f.CloseRequest(convert, convert ? 0 : 3);
        var first = await f.Close.Handle(request, default);
        var movements = f.Promotions[0].ContainerMovements.Count;
        f.Open = false;
        Assert.Equal(first, await f.Close.Handle(request, default));
        Assert.Equal(movements, f.Promotions[0].ContainerMovements.Count);
        Assert.Equal(convert ? 1 : 0, f.Customers.Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Second_closure_with_different_key_is_rejected(bool convert)
    {
        var f = new Fixture();
        await f.Register.Handle(f.Request(), default);
        await f.Close.Handle(f.CloseRequest(convert), default);
        await Assert.ThrowsAsync<ConflictException>(() => f.Close.Handle(f.CloseRequest(!convert), default));
    }

    [Fact]
    public async Task Retry_key_cannot_change_closure_target_or_outcome()
    {
        var f = new Fixture();
        await f.Register.Handle(f.Request(), default);
        var request = f.CloseRequest(true);
        await f.Close.Handle(request, default);
        await Assert.ThrowsAsync<ConflictException>(() => f.Close.Handle(request with { PromotionId = Guid.NewGuid() }, default));
        await Assert.ThrowsAsync<ConflictException>(() => f.Close.Handle(request with { ConvertToCustomer = false }, default));
    }

    [Fact]
    public async Task Other_vehicle_cannot_close_promotion()
    {
        var f = new Fixture();
        await f.Register.Handle(f.Request(), default);
        f.Session.VehicleId = Guid.NewGuid();
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Close.Handle(f.CloseRequest(true), default));
        Assert.Empty(f.Customers);
        Assert.Equal(PromotionStatus.Pending, f.Promotions[0].Status);
    }

    [Fact]
    public async Task Closure_requires_open_departure()
    {
        var f = new Fixture();
        await f.Register.Handle(f.Request(), default);
        f.Open = false;
        await Assert.ThrowsAsync<ConflictException>(() => f.Close.Handle(f.CloseRequest(false, 3), default));
        Assert.Equal(PromotionStatus.Pending, f.Promotions[0].Status);
    }

    [Fact]
    public async Task Excessive_or_unrelated_returns_are_rejected()
    {
        var f = new Fixture();
        await f.Register.Handle(f.Request(), default);
        await Assert.ThrowsAsync<BadRequestException>(() => f.Close.Handle(f.CloseRequest(false, 4), default));
        await Assert.ThrowsAsync<BadRequestException>(() => f.Close.Handle(f.CloseRequest(false) with
        {
            ContainersReturned = [new(Guid.NewGuid(), 1)]
        }, default));
        Assert.Equal(PromotionStatus.Pending, f.Promotions[0].Status);
        Assert.Single(f.Session.StockMovements);
    }

    [Fact]
    public async Task Invalid_conversion_data_is_rejected_before_creating_customer()
    {
        var f = new Fixture();
        await f.Register.Handle(f.Request(), default);
        await Assert.ThrowsAsync<GMSoft.Application.Common.Exceptions.ValidationException>(() => f.Close.Handle(f.CloseRequest(true) with
        {
            Customer = new(null, "", "1", "Calle", null, [])
        }, default));
        Assert.Empty(f.Customers);
        Assert.Equal(PromotionStatus.Pending, f.Promotions[0].Status);
    }

    [Theory]
    [InlineData(-1, PromotionStatus.Pending, "Overdue", true, false)]
    [InlineData(0, PromotionStatus.Pending, "Pending", false, true)]
    [InlineData(1, PromotionStatus.Pending, "Pending", false, false)]
    [InlineData(-1, PromotionStatus.Converted, "Converted", false, false)]
    [InlineData(-1, PromotionStatus.NotConverted, "NotConverted", false, false)]
    public void Overdue_is_derived_only_for_pending_promotions(int days, PromotionStatus status,
        string display, bool overdue, bool today)
    {
        var p = new Promotion { PickupDate = PromotionDto.Today.AddDays(days), Status = status };
        var dto = PromotionDto.From(p);
        Assert.Equal(display, dto.Status);
        Assert.Equal(overdue, dto.IsOverdue);
        Assert.Equal(today, dto.IsDueToday);
        Assert.Equal(status, p.Status);
    }

    [Fact]
    public void Registration_validator_reuses_required_customer_data_and_rejects_duplicate_products()
    {
        var f = new Fixture();
        var v = new RegisterPromotionCommandValidator();
        Assert.True(v.Validate(f.Request()).IsValid);
        Assert.False(v.Validate(f.Request() with { ClientRequestId = Guid.Empty }).IsValid);
        Assert.False(v.Validate(f.Request() with { Items = [] }).IsValid);
        Assert.False(v.Validate(f.Request() with { Items = null! }).IsValid);
        Assert.False(v.Validate(f.Request() with { Prospect = null! }).IsValid);
        Assert.False(v.Validate(f.Request() with { Items = [new(f.Product.Id, 1), new(f.Product.Id, 2)] }).IsValid);
        Assert.False(v.Validate(f.Request() with { Items = [new(f.Product.Id, 0)] }).IsValid);
        foreach (var days in new[] { Array.Empty<int>(), new[] { 1, 1 }, new[] { 0 }, new[] { 8 } })
            Assert.False(v.Validate(f.Request() with { Prospect = f.Request().Prospect with { VisitDays = days } }).IsValid);
    }

    [Fact]
    public void Closure_validator_rejects_incompatible_fields_and_duplicate_returns()
    {
        var v = new ClosePromotionCommandValidator();
        var id = Guid.NewGuid();
        var valid = new ClosePromotionCommand(id, Guid.NewGuid(), true, null, []);
        Assert.True(v.Validate(valid).IsValid);
        Assert.False(v.Validate(valid with { ContainersReturned = [new(id, 1)] }).IsValid);
        Assert.False(v.Validate(valid with { ContainersReturned = null! }).IsValid);
        Assert.False(v.Validate(valid with { ConvertToCustomer = false, ContainersReturned = [new(id, 1), new(id, 1)] }).IsValid);
        Assert.False(v.Validate(valid with { ConvertToCustomer = false, ContainersReturned = [new(id, -1)] }).IsValid);
        Assert.False(v.Validate(valid with { ClientRequestId = Guid.Empty }).IsValid);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1, true)]
    [InlineData(7, true)]
    [InlineData(int.MaxValue, true)]
    public void Settings_accept_only_positive_integers(int days, bool valid) =>
        Assert.Equal(valid, new UpdatePromotionSettingsCommandValidator().Validate(new UpdatePromotionSettingsCommand(days)).IsValid);

    [Fact]
    public async Task Settings_read_and_update_preserve_registered_pickup_date()
    {
        var f = new Fixture();
        var original = await f.Register.Handle(f.Request(), default);
        var get = new GetPromotionSettingsQueryHandler(f.Settings);
        Assert.Equal(7, (await get.Handle(new(), default)).PickupDays);
        var update = new UpdatePromotionSettingsCommandHandler(f.Settings);
        Assert.Equal(11, (await update.Handle(new(11), default)).PickupDays);
        Assert.Equal(11, (await get.Handle(new(), default)).PickupDays);
        Assert.Equal(original.PickupDate, f.Promotions[0].PickupDate);
    }

    [Fact]
    public async Task Out_of_range_configured_date_is_rejected_without_stock_movements()
    {
        var f = new Fixture { PickupDays = int.MaxValue };
        await Assert.ThrowsAsync<ConflictException>(() => f.Register.Handle(f.Request(), default));
        Assert.Empty(f.Promotions);
        Assert.Empty(f.Session.StockMovements);
    }

    [Fact]
    public void List_validates_pagination_status_and_date_range()
    {
        var v = new GetPromotionsQueryValidator();
        Assert.True(v.Validate(new GetPromotionsQuery(Status: "Overdue")).IsValid);
        Assert.False(v.Validate(new GetPromotionsQuery(Page: 0)).IsValid);
        Assert.False(v.Validate(new GetPromotionsQuery(PageSize: 101)).IsValid);
        Assert.False(v.Validate(new GetPromotionsQuery(Status: "Anything")).IsValid);
        Assert.False(v.Validate(new GetPromotionsQuery(From: new(2026, 10, 8), To: new(2026, 10, 7))).IsValid);
    }

    private sealed class Fixture
    {
        public Guid DriverId = Guid.NewGuid();
        public bool Open = true;
        public int Stock = 20, Balance, PickupDays = 7;
        public Product Product = new() { Id = Guid.NewGuid(), Detail = "Bidon", Tracking = ContainerTracking.ByBalance };
        public DeliverySession Session = new() { Id = Guid.NewGuid(), VehicleId = Guid.NewGuid(), ZoneId = Guid.NewGuid() };
        public List<Promotion> Promotions = [];
        public List<Customer> Customers = [];
        public RegisterPromotionCommandHandler Register { get; }
        public ClosePromotionCommandHandler Close { get; }
        public IPromotionSettingsRepository Settings { get; }

        public Fixture()
        {
            var promotions = Stub<IPromotionRepository>((name, args) => name switch
            {
                "LockAsync" => Task.CompletedTask,
                "FindRequestAsync" => Task.FromResult(Promotions.FirstOrDefault(p =>
                    (bool)args![1]! ? p.CloseClientRequestId == (Guid)args[0]! : p.ClientRequestId == (Guid)args[0]!)),
                "GetDetailsAsync" => Task.FromResult(Promotions.FirstOrDefault(p => p.Id == (Guid)args![0]!)),
                "AddAsync" => AddPromotion((Promotion)args![0]!),
                "Update" => null,
                _ => throw new InvalidOperationException(name)
            });
            var sessions = Stub<ISessionRepository>((name, _) => name switch
            {
                "GetOpenByDriverAsync" => Task.FromResult<DeliverySession?>(Open ? Session : null),
                "GetStockBalanceAsync" => Task.FromResult<IReadOnlyList<SessionStockLineDto>>([new(Product.Id, Product.Detail, Stock, 0)]),
                "Update" => null,
                _ => throw new InvalidOperationException(name)
            });
            Settings = Stub<IPromotionSettingsRepository>((name, args) => name switch
            {
                "GetPickupDaysAsync" => Task.FromResult(PickupDays),
                "SetPickupDaysAsync" => SetDays((int)args![0]!),
                _ => throw new InvalidOperationException(name)
            });
            var customers = Stub<ICustomerRepository>((name, args) => name switch
            {
                "GetNextRouteOrderAsync" => Task.FromResult(8),
                "AddAsync" => AddCustomer((Customer)args![0]!),
                _ => throw new InvalidOperationException(name)
            });
            var user = Stub<ICurrentUserService>((_, _) => (Guid?)DriverId);
            var uow = Stub<IUnitOfWork>((name, args) => name switch
            {
                "ExecuteInTransactionAsync" => ((Func<Task>)args![0]!)(),
                "SaveChangesAsync" => Task.FromResult(1),
                _ => throw new InvalidOperationException(name)
            });
            Register = new(promotions, Settings, sessions,
                Stub<IProductRepository>((_, _) => Task.FromResult<Product?>(Product)), user, uow);
            Close = new(promotions, sessions, customers,
                Stub<IContainerBalanceRepository>((name, args) => name == "AdjustAsync"
                    ? Adjust((int)args![2]!) : throw new InvalidOperationException(name)), user, uow);
        }

        private Task AddPromotion(Promotion p) { p.Id = Guid.NewGuid(); Promotions.Add(p); return Task.CompletedTask; }
        private Task AddCustomer(Customer c) { c.Id = Guid.NewGuid(); Customers.Add(c); return Task.CompletedTask; }
        private Task Adjust(int delta) { Balance += delta; return Task.CompletedTask; }
        private Task SetDays(int days) { PickupDays = days; return Task.CompletedTask; }
        public RegisterPromotionCommand Request() => new(new(null, " Ana ", "123", "Calle 1", null, [5, 2]),
            [new(Product.Id, 3)], Guid.NewGuid());
        public ClosePromotionCommand CloseRequest(bool convert, int returned = 0) =>
            new(Promotions.Single().Id, Guid.NewGuid(), convert, null, returned > 0 ? [new(Product.Id, returned)] : []);
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

