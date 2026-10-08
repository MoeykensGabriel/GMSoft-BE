using System.Reflection;
using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Sessions.AddStock;
using GMSoft.Application.Features.Sessions.Close;
using GMSoft.Application.Features.Sessions.Common;
using GMSoft.Application.Features.Sessions.GetById;
using GMSoft.Application.Features.Sessions.GetCurrent;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;

namespace GMSoft.Application.Tests.Features.Sessions;

public class RestockWorkflowTests
{
    [Fact]
    public async Task Multiple_products_enter_together_with_one_timestamp_and_update_current_stock()
    {
        var f = new Fixture();
        f.Movement(f.First, 5, SessionStockMovementType.InitialLoad);
        var result = await f.Handler.Handle(f.Request(), default);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(f.Session.Id, result.SessionId);
        Assert.Equal(f.UserId, result.RegisteredByUserId);
        Assert.Equal("En ruta", result.Notes);
        Assert.Equal(DateTimeKind.Utc, result.OccurredAt.Kind);
        Assert.Equal(0, result.OccurredAt.Ticks % 10);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, f.Transactions);
        Assert.Equal(1, f.Saves);
        Assert.All(f.Session.Restocks.Single().Items, m =>
        {
            Assert.Equal(result.OccurredAt, m.OccurredAt);
            Assert.Equal(ContainerState.Full, m.State);
            Assert.Equal(SessionStockMovementType.Restock, m.Type);
            Assert.Equal(f.UserId, m.RegisteredByUserId);
        });
        var current = await new GetCurrentSessionQueryHandler(f.Sessions, f.User(false))
            .Handle(new(), default);
        Assert.NotNull(current);
        Assert.Equal(8, current.Stock.Single(s => s.ProductId == f.First.Id).FullOnBoard);
        Assert.Equal(2, current.Stock.Single(s => s.ProductId == f.Second.Id).FullOnBoard);
        Assert.All(current.Stock, s => Assert.Equal(0, s.EmptyOnBoard));
        Assert.Equal(result.Id, Assert.Single(current.Restocks).Id);
    }

    [Fact]
    public async Task Retry_returns_original_even_after_closure_and_catalog_changes()
    {
        var f = new Fixture();
        var request = f.Request();
        var first = await f.Handler.Handle(request, default);
        f.Session.Status = SessionStatus.Closed;
        f.First.Detail = "Nombre nuevo";
        f.First.IsPublished = false;
        var retry = await f.Handler.Handle(request with { Items = [new(f.First.Id, 100)], Notes = "Cambio" }, default);
        Assert.Equal(first.Id, retry.Id);
        Assert.Equal(first.OccurredAt, retry.OccurredAt);
        Assert.Equal(first.Items.ToArray(), retry.Items.ToArray());
        Assert.Equal(first.Notes, retry.Notes);
        Assert.Equal(1, f.Saves);
        Assert.Equal(2, f.Session.StockMovements.Count);
    }

    [Fact]
    public async Task Unique_index_collision_returns_winner_without_adding_loser_stock()
    {
        var f = new Fixture();
        var request = f.Request();
        var first = await f.Handler.Handle(request, default);
        f.HideNextLookup = true;
        f.Collision = true;
        var retry = await f.Handler.Handle(request, default);
        Assert.Equal(first.Id, retry.Id);
        Assert.Equal(first.OccurredAt, retry.OccurredAt);
        Assert.Single(f.Session.Restocks);
        Assert.Equal(2, f.Session.StockMovements.Count);
    }

    [Fact]
    public async Task Request_id_cannot_be_reused_on_another_session()
    {
        var f = new Fixture();
        var request = f.Request();
        await f.Handler.Handle(request, default);
        await Assert.ThrowsAsync<ConflictException>(() => f.Handler.Handle(request with { Id = Guid.NewGuid() }, default));
        Assert.Equal(1, f.Saves);
    }

    [Fact]
    public async Task Closed_session_rejects_new_restock()
    {
        var f = new Fixture(); f.Session.Status = SessionStatus.Closed;
        await Assert.ThrowsAsync<ConflictException>(() => f.Handler.Handle(f.Request(), default));
        Assert.Empty(f.Session.Restocks);
        Assert.Equal(0, f.Saves);
    }

    [Fact]
    public async Task Reception_between_initial_read_and_transaction_rejects_restock()
    {
        var f = new Fixture(); f.CloseBeforeLock = true;
        await Assert.ThrowsAsync<ConflictException>(() => f.Handler.Handle(f.Request(), default));
        Assert.Empty(f.Session.Restocks);
        Assert.Empty(f.Session.StockMovements);
        Assert.Equal(0, f.Saves);
    }

    [Fact]
    public async Task Missing_session_is_not_found()
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<NotFoundException>(() => f.Handler.Handle(f.Request() with { Id = Guid.NewGuid() }, default));
        Assert.Equal(0, f.Saves);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Missing_or_unpublished_product_rejects_entire_batch(bool missing)
    {
        var f = new Fixture();
        if (missing) f.Products.Remove(f.Second.Id); else f.Second.IsPublished = false;
        if (missing) await Assert.ThrowsAsync<NotFoundException>(() => f.Handler.Handle(f.Request(), default));
        else await Assert.ThrowsAsync<BadRequestException>(() => f.Handler.Handle(f.Request(), default));
        Assert.Empty(f.Session.StockMovements);
        Assert.Empty(f.Session.Restocks);
        Assert.Equal(0, f.Saves);
    }

    [Fact]
    public async Task Driver_cannot_register_even_a_retry()
    {
        var f = new Fixture();
        var request = f.Request();
        await f.Handler.Handle(request, default);
        var handler = new AddSessionStockCommandHandler(f.Sessions, f.Restocks, f.ProductRepository, f.User(false), f.Unit);
        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(request, default));
        Assert.Equal(1, f.Saves);
    }

    [Fact]
    public async Task ByUnit_matches_initial_load_which_allows_it()
    {
        var f = new Fixture(); f.First.Tracking = ContainerTracking.ByUnit;
        var result = await f.Handler.Handle(f.Request(), default);
        Assert.Contains(result.Items, i => i.ProductId == f.First.Id && i.Quantity == 3);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("null")]
    [InlineData("nullItem")]
    [InlineData("duplicate")]
    [InlineData("zero")]
    [InlineData("negative")]
    [InlineData("product")]
    [InlineData("requestId")]
    [InlineData("session")]
    [InlineData("notes")]
    public void Invalid_requests_are_rejected(string invalid)
    {
        var f = new Fixture(); var request = f.Request();
        request = invalid switch
        {
            "empty" => request with { Items = [] },
            "null" => request with { Items = null! },
            "nullItem" => request with { Items = [null!] },
            "duplicate" => request with { Items = [new(f.First.Id, 1), new(f.First.Id, 2)] },
            "zero" => request with { Items = [new(f.First.Id, 0)] },
            "negative" => request with { Items = [new(f.First.Id, -1)] },
            "product" => request with { Items = [new(Guid.Empty, 1)] },
            "requestId" => request with { ClientRequestId = Guid.Empty },
            "session" => request with { Id = Guid.Empty },
            _ => request with { Notes = new string('x', 501) }
        };
        Assert.False(new AddSessionStockCommandValidator().Validate(request).IsValid);
    }

    [Fact]
    public void Valid_batch_and_optional_notes_pass_validation()
    {
        var f = new Fixture();
        Assert.True(new AddSessionStockCommandValidator().Validate(f.Request() with { Notes = null }).IsValid);
    }

    [Theory]
    [InlineData(11, true, 0)]
    [InlineData(10, false, 1)]
    public async Task Reception_uses_initial_load_plus_two_restocks_minus_sales_and_keeps_empties_separate(
        int fullReturned, bool balances, int shortage)
    {
        var f = new Fixture();
        f.Movement(f.First, 10, SessionStockMovementType.InitialLoad);
        // Un Restock historico sin cabecera tambien cuenta.
        f.Movement(f.First, 1, SessionStockMovementType.Restock);
        await f.Handler.Handle(f.Request() with { Items = [new(f.First.Id, 3)] }, default);
        await f.Handler.Handle(f.Request() with { Items = [new(f.First.Id, 2)] }, default);
        f.Movement(f.First, -5, SessionStockMovementType.Delivered);
        f.Movement(f.First, 4, SessionStockMovementType.CollectedEmpty, ContainerState.Empty);
        var before = await new GetSessionByIdQueryHandler(f.Sessions, f.User(true)).Handle(new(f.Session.Id), default);
        Assert.Equal(11, Assert.Single(before.Stock).FullOnBoard);
        Assert.Equal(4, Assert.Single(before.Stock).EmptyOnBoard);
        Assert.Equal(2, before.Restocks.Count);
        Assert.Equal(before.Restocks.OrderBy(r => r.OccurredAt).ThenBy(r => r.Id), before.Restocks);
        var vehicles = Stub<IVehicleRepository>((name, _) => name switch
        {
            "GetByIdAsync" => Task.FromResult<Vehicle?>(new Vehicle { Id = f.Session.VehicleId }),
            "Update" => null,
            _ => throw new InvalidOperationException(name)
        });
        var result = await new CloseSessionCommandHandler(f.Sessions, vehicles, f.User(true), f.Unit)
            .Handle(new(f.Session.Id, 110, [new(f.First.Id, ContainerState.Full, fullReturned),
                new(f.First.Id, ContainerState.Empty, 4)]), default);
        Assert.Equal(balances, result.CuadraTodo);
        Assert.Equal(shortage, result.Faltante.Sum(s => s.FullOnBoard));
        Assert.All(result.Faltante, s => Assert.Equal(0, s.EmptyOnBoard));
    }

    [Fact]
    public async Task Driver_sees_only_own_open_session_restocks_and_admin_can_read_them_after_closure()
    {
        var f = new Fixture();
        var own = await f.Handler.Handle(f.Request(), default);
        var other = new Fixture(); await other.Handler.Handle(other.Request(), default);
        var sessions = Stub<ISessionRepository>((name, args) => name switch
        {
            "GetOpenByDriverAsync" => Task.FromResult<DeliverySession?>(
                (Guid)args![0]! == f.Session.DriverId && f.Session.Status == SessionStatus.Open ? f.Session : null),
            "GetWithDetailsAsync" => Task.FromResult<DeliverySession?>((Guid)args![0]! == f.Session.Id ? f.Session : other.Session),
            "GetStockBalanceAsync" => Task.FromResult(f.Stock()),
            _ => throw new InvalidOperationException(name)
        });
        var query = new GetCurrentSessionQueryHandler(sessions, f.User(false));
        Assert.Equal(own.Id, Assert.Single((await query.Handle(new(), default))!.Restocks).Id);
        await Assert.ThrowsAsync<ForbiddenException>(() => new GetSessionByIdQueryHandler(sessions, f.User(false))
            .Handle(new(other.Session.Id), default));
        f.Session.Status = SessionStatus.Closed;
        Assert.Null(await query.Handle(new(), default));
        var admin = await new GetSessionByIdQueryHandler(sessions, f.User(true)).Handle(new(f.Session.Id), default);
        Assert.Equal(own.Id, Assert.Single(admin.Restocks).Id);
    }

    private sealed class Fixture
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public DeliverySession Session { get; } = new() { Id = Guid.NewGuid(), DriverId = Guid.NewGuid(),
            VehicleId = Guid.NewGuid(), OpenedAt = DateTime.UtcNow, KilometersAtOpen = 100 };
        public Product First { get; } = new() { Id = Guid.NewGuid(), Detail = "Bidon 20 litros", IsPublished = true, Tracking = ContainerTracking.ByBalance };
        public Product Second { get; } = new() { Id = Guid.NewGuid(), Detail = "Agua 2 litros", IsPublished = true };
        public Dictionary<Guid, Product> Products { get; }
        public ISessionRepository Sessions { get; }
        public ISessionRestockRepository Restocks { get; }
        public IRepository<Product> ProductRepository { get; }
        public IUnitOfWork Unit { get; }
        public AddSessionStockCommandHandler Handler { get; }
        public int Transactions { get; private set; }
        public int Saves { get; private set; }
        public bool HideNextLookup { get; set; }
        public bool Collision { get; set; }
        public bool CloseBeforeLock { get; set; }
        private SessionRestock? pending;

        public Fixture()
        {
            Products = new() { [First.Id] = First, [Second.Id] = Second };
            Sessions = Stub<ISessionRepository>((name, args) => name switch
            {
                "GetByIdAsync" or "GetWithDetailsAsync" => Task.FromResult<DeliverySession?>(
                    (Guid)args![0]! == Session.Id ? Session : null),
                "GetOpenByDriverAsync" => Task.FromResult<DeliverySession?>(Session.Status == SessionStatus.Open ? Session : null),
                "GetStockBalanceAsync" => Task.FromResult(Stock()),
                "Update" => null,
                _ => throw new InvalidOperationException(name)
            });
            Restocks = Stub<ISessionRestockRepository>((name, args) =>
            {
                if (name == "IsSessionOpenForUpdateAsync")
                {
                    if (CloseBeforeLock) Session.Status = SessionStatus.Closed;
                    return Task.FromResult(Session.Status == SessionStatus.Open);
                }
                if (name == "GetByClientRequestAsync")
                {
                    if (HideNextLookup) { HideNextLookup = false; return Task.FromResult<SessionRestock?>(null); }
                    return Task.FromResult(Session.Restocks.SingleOrDefault(r => r.ClientRequestId == (Guid)args![0]!));
                }
                if (name == "AddAsync") { pending = (SessionRestock)args![0]!; return Task.CompletedTask; }
                throw new InvalidOperationException(name);
            });
            ProductRepository = Stub<IRepository<Product>>((name, args) => name == "GetByIdAsync"
                ? Task.FromResult(Products.GetValueOrDefault((Guid)args![0]!)) : throw new InvalidOperationException(name));
            Unit = Stub<IUnitOfWork>((name, args) =>
            {
                if (name == "ExecuteInTransactionAsync") { Transactions++; return ((Func<Task>)args![0]!)(); }
                if (name == "SaveChangesAsync")
                {
                    if (Collision) { pending = null; throw new DuplicateRestockRequestException(new Exception("unique index")); }
                    Saves++;
                    if (pending is not null)
                    {
                        pending.Id = Guid.NewGuid();
                        Session.Restocks.Add(pending);
                        foreach (var item in pending.Items) Session.StockMovements.Add(item);
                        pending = null;
                    }
                    return Task.FromResult(1);
                }
                throw new InvalidOperationException(name);
            });
            Handler = new(Sessions, Restocks, ProductRepository, User(true), Unit);
        }

        public AddSessionStockCommand Request() => new(Session.Id, [new(First.Id, 3), new(Second.Id, 2)], Guid.NewGuid(), "  En ruta  ");
        public ICurrentUserService User(bool admin) => Stub<ICurrentUserService>((name, args) => name switch
        {
            "get_UserId" => UserId,
            "get_DriverId" => Session.DriverId,
            "IsInRole" => admin && (string)args![0]! == AppRoles.Admin,
            _ => null
        });
        public void Movement(Product product, int quantity, SessionStockMovementType type, ContainerState state = ContainerState.Full)
            => Session.StockMovements.Add(new() { DeliverySessionId = Session.Id, ProductId = product.Id,
                Quantity = quantity, Type = type, State = state });
        public IReadOnlyList<SessionStockLineDto> Stock() => Session.StockMovements.GroupBy(m => m.ProductId)
            .Select(g => new SessionStockLineDto(g.Key, Products[g.Key].Detail,
                g.Where(m => m.State == ContainerState.Full).Sum(m => m.Quantity),
                g.Where(m => m.State == ContainerState.Empty).Sum(m => m.Quantity))).ToList();
    }

    private static T Stub<T>(Func<string, object?[]?, object?> invoke) where T : class
    {
        var proxy = DispatchProxy.Create<T, RestockProxy>();
        ((RestockProxy)(object)proxy).Handler = invoke;
        return proxy;
    }
    public class RestockProxy : DispatchProxy
    {
        public Func<string, object?[]?, object?> Handler { get; set; } = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!.Name, args);
    }
}
