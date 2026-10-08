using System.Reflection;
using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Sessions.Close;
using GMSoft.Application.Features.Sessions.Common;
using GMSoft.Application.Features.Sessions.DailySummary;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;

namespace GMSoft.Application.Tests.Features.Sessions;

public class DailySummaryTests
{
    [Fact]
    public async Task Open_departure_includes_restock_sales_both_promotions_and_returns_without_double_counting()
    {
        var f = new Fixture();
        f.LoadTrip();
        var result = Assert.Single((await f.Query()).Sessions);
        Assert.False(result.IsClosed);
        Assert.Null(result.ClosedAt);
        Assert.Null(result.Money.CashDeclared);
        Assert.Null(result.Money.CashDifference);
        Assert.Equal(new StockSummaryDto(15, 6, 0, 9, 5, 0, 5), result.Totals);
        var balance = Assert.Single(await f.Sessions.GetStockBalanceAsync(f.Session.Id));
        Assert.Equal(balance.FullOnBoard, result.Totals.FullDifference);
        Assert.Equal(balance.EmptyOnBoard, result.Totals.EmptyDifference);
        Assert.Equal(7, f.Session.StockMovements.Count); // La consulta no registra nada.
    }

    [Theory]
    [InlineData(9, 5, true, 0, 0)]
    [InlineData(7, 3, false, 2, 2)]
    [InlineData(10, 6, false, -1, -1)]
    public async Task Closed_differences_match_the_actual_reception_result(int full, int empty,
        bool balances, int fullDifference, int emptyDifference)
    {
        var f = new Fixture(); f.LoadTrip();
        var close = await f.Close(full, empty);
        var summary = Assert.Single((await f.Query()).Sessions);
        Assert.True(summary.IsClosed);
        Assert.NotNull(summary.ClosedAt);
        Assert.Equal(150, summary.KilometersAtClose);
        Assert.Equal(balances, close.CuadraTodo);
        Assert.Equal(fullDifference, summary.Totals.FullDifference);
        Assert.Equal(emptyDifference, summary.Totals.EmptyDifference);
        Assert.Equal(close.Faltante.Sum(x => x.FullOnBoard), summary.Totals.FullDifference);
        Assert.Equal(close.Faltante.Sum(x => x.EmptyOnBoard), summary.Totals.EmptyDifference);
    }

    [Theory]
    [InlineData(100, 0)]
    [InlineData(80, 20)]
    [InlineData(120, -20)]
    public async Task Settlement_compares_only_cash_and_preserves_notes_and_received_time(int declared, int difference)
    {
        var f = new Fixture(); f.LoadTrip(); await f.Close(9, 5);
        f.Payments.AddRange([new(f.Session.Id, PaymentMethod.Cash, 40),
            new(f.Session.Id, PaymentMethod.Cash, 60), new(f.Session.Id, PaymentMethod.Transfer, 50),
            new(f.Session.Id, PaymentMethod.Card, 30)]);
        f.Session.CashSettlement = new() { AmountReceived = declared, Notes = "Contado en oficina", ReceivedAt = f.Session.ClosedAt!.Value };
        var result = Assert.Single((await f.Query()).Sessions);
        Assert.Equal(new MoneyDailySummaryDto(100, 50, 30, declared, difference), result.Money);
        Assert.Equal("Contado en oficina", result.Notes);
        Assert.Equal(f.Session.ClosedAt, result.ReceivedAt);
    }

    [Fact]
    public async Task Adjustments_and_transfers_of_both_states_preserve_ledger_identity()
    {
        var f = new Fixture(); f.LoadTrip();
        foreach (var state in new[] { ContainerState.Full, ContainerState.Empty })
        {
            f.Move(3, SessionStockMovementType.Adjustment, state);
            f.Move(-2, SessionStockMovementType.Adjustment, state);
            f.Move(4, SessionStockMovementType.TransferIn, state);
            f.Move(-1, SessionStockMovementType.TransferOut, state);
        }
        var open = Assert.Single((await f.Query()).Sessions);
        Assert.Equal(19, open.Totals.FullLoaded);
        Assert.Equal(9, open.Totals.EmptyCollected);
        var stock = Assert.Single(await f.Sessions.GetStockBalanceAsync(f.Session.Id));
        Assert.Equal(stock.FullOnBoard, open.Totals.FullDifference);
        Assert.Equal(stock.EmptyOnBoard, open.Totals.EmptyDifference);
        var close = await f.Close(12, 8);
        var closed = Assert.Single((await f.Query()).Sessions);
        Assert.Equal(Assert.Single(close.Faltante).FullOnBoard, closed.Totals.FullDifference);
        Assert.Equal(Assert.Single(close.Faltante).EmptyOnBoard, closed.Totals.EmptyDifference);
    }

    [Fact]
    public async Task Day_uses_argentine_opening_boundaries_orders_departures_and_excludes_other_vehicle()
    {
        var f = new Fixture(); f.LoadTrip();
        f.Session.OpenedAt = Fixture.Start;
        // 23:59 Argentina es el dia siguiente en UTC; entra aunque cierre otro dia.
        var second = f.NewSession(Fixture.Start.AddDays(1).AddTicks(-1));
        var onlyEmpty = new Product { Id = Guid.NewGuid(), Detail = "Agua" };
        second.StockMovements.Add(new() { ProductId = f.Product.Id, Product = f.Product, Quantity = 2,
            State = ContainerState.Full, Type = SessionStockMovementType.InitialLoad });
        second.StockMovements.Add(new() { ProductId = onlyEmpty.Id, Product = onlyEmpty, Quantity = 3,
            State = ContainerState.Empty, Type = SessionStockMovementType.CollectedEmpty });
        f.All.Insert(0, second);
        f.All.Add(f.NewSession(Fixture.Start.AddTicks(-1)));
        f.All.Add(f.NewSession(Fixture.Start.AddDays(1)));
        var other = f.NewSession(Fixture.Start.AddHours(1)); other.VehicleId = Guid.NewGuid(); f.All.Add(other);
        f.Payments.AddRange([new(f.Session.Id, PaymentMethod.Cash, 100), new(second.Id, PaymentMethod.Cash, 50),
            new(second.Id, PaymentMethod.Transfer, 30), new(other.Id, PaymentMethod.Cash, 999)]);
        f.Session.CashSettlement = new() { AmountReceived = 100 };
        var result = await f.Query();
        Assert.Equal(new[] { f.Session.Id, second.Id }, result.Sessions.Select(s => s.SessionId));
        Assert.Equal(new StockSummaryDto(17, 6, 0, 11, 8, 0, 8), result.DayTotals.Totals);
        Assert.Equal(new[] { "Agua", "Bidon" }, result.DayTotals.Products.Select(p => p.ProductDetail));
        Assert.Equal(2, result.DayTotals.Products.Count);
        Assert.Equal(new MoneyDailySummaryDto(150, 30, 0, null, null), result.DayTotals.Money);
        Assert.Equal(1, result.DayTotals.PendingSettlements);
        Assert.False(result.DayTotals.IsClosed);
        second.CashSettlement = new() { AmountReceived = 40 };
        f.Session.Status = second.Status = SessionStatus.Closed;
        result = await f.Query();
        Assert.True(result.DayTotals.IsClosed);
        Assert.Equal(new MoneyDailySummaryDto(150, 30, 0, 140, 10), result.DayTotals.Money);
    }

    [Fact]
    public async Task Empty_day_returns_empty_lists_and_zero_totals()
    {
        var f = new Fixture(); f.All.Clear();
        var result = await f.Query();
        Assert.Empty(result.Sessions);
        Assert.Empty(result.DayTotals.Products);
        Assert.Equal(new StockSummaryDto(0, 0, 0, 0, 0, 0, 0), result.DayTotals.Totals);
        Assert.Equal(new MoneyDailySummaryDto(0, 0, 0, 0, 0), result.DayTotals.Money);
    }

    [Fact]
    public async Task Non_admin_is_rejected_before_reading()
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new GetDailySummaryQueryHandler(f.Repository, Stub<ICurrentUserService>((_, _) => false))
                .Handle(new(f.Session.VehicleId, new(2026, 10, 8)), default));
        Assert.Equal(0, f.Reads);
    }

    [Fact]
    public void Vehicle_and_representable_date_are_required()
    {
        var validator = new GetDailySummaryQueryValidator();
        Assert.True(validator.Validate(new GetDailySummaryQuery(Guid.NewGuid(), new(2026, 10, 8))).IsValid);
        Assert.False(validator.Validate(new GetDailySummaryQuery(Guid.Empty, new(2026, 10, 8))).IsValid);
        Assert.False(validator.Validate(new GetDailySummaryQuery(Guid.NewGuid(), default)).IsValid);
        Assert.False(validator.Validate(new GetDailySummaryQuery(Guid.NewGuid(), DateOnly.MaxValue)).IsValid);
    }

    private sealed class Fixture
    {
        public static readonly DateTime Start = new(2026, 10, 8, 3, 0, 0, DateTimeKind.Utc);
        public Product Product { get; } = new() { Id = Guid.NewGuid(), Detail = "Bidon" };
        public DeliverySession Session { get; }
        public List<DeliverySession> All { get; } = [];
        public List<DailySummaryPayment> Payments { get; } = [];
        public ISessionRepository Sessions { get; }
        public IDailySummaryRepository Repository { get; }
        public int Reads { get; private set; }
        private readonly ICurrentUserService user = Stub<ICurrentUserService>((name, args) => name switch
        {
            "IsInRole" => (string)args![0]! == AppRoles.Admin,
            "get_UserId" => (Guid?)Guid.NewGuid(),
            _ => null
        });
        public Fixture()
        {
            Session = NewSession(Start.AddHours(5)); All.Add(Session);
            Repository = Stub<IDailySummaryRepository>((name, args) =>
            {
                Assert.Equal("GetAsync", name); Reads++;
                Assert.Equal(Start, args![1]); Assert.Equal(Start.AddDays(1), args[2]);
                var selected = All.Where(s => s.VehicleId == (Guid)args[0]! &&
                    s.OpenedAt >= (DateTime)args[1]! && s.OpenedAt < (DateTime)args[2]!).ToList();
                return Task.FromResult(new DailySummaryData(selected,
                    Payments.Where(p => selected.Any(s => s.Id == p.SessionId)).ToList()));
            });
            Sessions = Stub<ISessionRepository>((name, _) => name switch
            {
                "GetByIdAsync" => Task.FromResult<DeliverySession?>(Session),
                "Update" => null,
                "GetStockBalanceAsync" => Task.FromResult<IReadOnlyList<SessionStockLineDto>>(
                    Session.StockMovements.GroupBy(m => m.ProductId).Select(g => new SessionStockLineDto(g.Key, Product.Detail,
                        g.Where(m => m.State == ContainerState.Full).Sum(m => m.Quantity),
                        g.Where(m => m.State == ContainerState.Empty).Sum(m => m.Quantity))).ToList()),
                _ => throw new InvalidOperationException(name)
            });
        }
        public DeliverySession NewSession(DateTime opened) => new() { Id = Guid.NewGuid(),
            VehicleId = Session?.VehicleId ?? Guid.NewGuid(), Vehicle = new() { Name = "Camion 1", LicensePlate = "AA123BB" },
            Driver = new() { FirstName = "Juan", LastName = "Perez" }, Zone = new() { Name = "Centro" },
            OpenedAt = opened, KilometersAtOpen = 100 };
        public void Move(int quantity, SessionStockMovementType type, ContainerState state = ContainerState.Full,
            Guid? deliveryId = null) => Session.StockMovements.Add(new() { ProductId = Product.Id, Product = Product,
                Quantity = quantity, Type = type, State = state, DeliveryId = deliveryId });
        public void LoadTrip()
        {
            Move(10, SessionStockMovementType.InitialLoad);
            Move(5, SessionStockMovementType.Restock);
            Move(-3, SessionStockMovementType.Delivered, deliveryId: Guid.NewGuid()); // Venta
            Move(-1, SessionStockMovementType.Delivered, deliveryId: Guid.NewGuid()); // Promotion historica
            Move(-2, SessionStockMovementType.Delivered); // Prueba sin Delivery
            Move(3, SessionStockMovementType.CollectedEmpty, ContainerState.Empty, Guid.NewGuid());
            Move(2, SessionStockMovementType.CollectedEmpty, ContainerState.Empty); // Retiro de prueba
        }
        public Task<DailySummaryDto> Query() => new GetDailySummaryQueryHandler(Repository, user)
            .Handle(new(Session.VehicleId, new(2026, 10, 8)), default);
        public async Task<CloseSessionResult> Close(int full, int empty)
        {
            var vehicles = Stub<IVehicleRepository>((name, _) => name == "GetByIdAsync"
                ? Task.FromResult<Vehicle?>(Session.Vehicle) : null);
            var unit = Stub<IUnitOfWork>((name, args) => name switch
            {
                "ExecuteInTransactionAsync" => ((Func<Task>)args![0]!)(),
                "SaveChangesAsync" => Task.FromResult(1),
                _ => throw new InvalidOperationException(name)
            });
            var result = await new CloseSessionCommandHandler(Sessions, vehicles, user, unit).Handle(
                new(Session.Id, 150, [new(Product.Id, ContainerState.Full, full), new(Product.Id, ContainerState.Empty, empty)]), default);
            foreach (var movement in Session.StockMovements) movement.Product = Product;
            return result;
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
