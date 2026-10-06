using System.Reflection;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Sessions.ActiveDepartures;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;

namespace GMSoft.Application.Tests.Features.Sessions;

public class ActiveDeparturesTests
{
    [Fact]
    public void Initial_load_combines_batches_and_excludes_sales_returns_restock_and_adjustments()
    {
        var session = Departure();
        var bidon = new Product { Id = Guid.NewGuid(), Detail = "Bidón 20 L" };
        var soda = new Product { Id = Guid.NewGuid(), Detail = "Soda" };
        void Add(Product product, int quantity, SessionStockMovementType type, ContainerState state = ContainerState.Full)
            => session.StockMovements.Add(new() { ProductId = product.Id, Product = product, Quantity = quantity, Type = type, State = state });
        Add(bidon, 10, SessionStockMovementType.InitialLoad);
        Add(bidon, 5, SessionStockMovementType.InitialLoad);
        Add(soda, 8, SessionStockMovementType.InitialLoad);
        Add(bidon, -4, SessionStockMovementType.Delivered);
        Add(bidon, 12, SessionStockMovementType.Restock);
        Add(bidon, 4, SessionStockMovementType.CollectedEmpty, ContainerState.Empty);
        Add(bidon, -1, SessionStockMovementType.Adjustment);
        Add(bidon, -10, SessionStockMovementType.ReturnedAtClose);
        Add(bidon, 3, SessionStockMovementType.InitialLoad, ContainerState.Empty);

        var dto = ActiveDepartureMapping.ToDto(session);
        Assert.Equal(2, dto.InitialLoad.Count);
        Assert.Equal(15, dto.InitialLoad.Single(line => line.ProductId == bidon.Id).Quantity);
        Assert.Equal(8, dto.InitialLoad.Single(line => line.ProductId == soda.Id).Quantity);
        Assert.Equal(session.Id, dto.SessionId);
        Assert.Equal("Camión 1", dto.VehicleName);
        Assert.Equal("Ana Pérez", dto.DriverName);
        Assert.Equal("Centro", dto.ZoneName);
    }

    [Fact]
    public void Missing_legacy_load_is_empty_instead_of_showing_current_stock_as_initial()
        => Assert.Empty(ActiveDepartureMapping.ToDto(Departure()).InitialLoad);

    [Fact]
    public async Task All_open_departures_are_returned_including_previous_days_and_without_pagination()
    {
        var old = Departure(); old.OpenedAt = DateTime.UtcNow.AddDays(-2);
        var recent = Departure(); recent.Vehicle!.Name = "Camión 2";
        var closed = Departure(); closed.Status = SessionStatus.Closed;
        var repository = Stub<ISessionRepository>((name, _) => name == "GetOpenWithInitialLoadAsync"
            ? Task.FromResult<IReadOnlyList<DeliverySession>>([recent, closed, old])
            : throw new InvalidOperationException(name));
        var result = await new GetActiveDeparturesQueryHandler(repository).Handle(new(), default);
        Assert.Equal(new[] { old.Id, recent.Id }, result.Select(d => d.SessionId));
        Assert.Equal(old.OpenedAt, result[0].OpenedAt);
    }

    [Fact]
    public async Task Empty_fleet_returns_empty_list()
    {
        var repository = Stub<ISessionRepository>((_, _) => Task.FromResult<IReadOnlyList<DeliverySession>>([]));
        Assert.Empty(await new GetActiveDeparturesQueryHandler(repository).Handle(new(), default));
    }

    private static DeliverySession Departure() => new()
    {
        Id = Guid.NewGuid(), VehicleId = Guid.NewGuid(), Status = SessionStatus.Open,
        Vehicle = new() { Name = "Camión 1", LicensePlate = "TEST001" },
        Driver = new() { FirstName = "Ana", LastName = "Pérez" },
        Zone = new() { Name = "Centro" }, OpenedAt = DateTime.UtcNow
    };
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
