using GMSoft.Application.Features.Deliveries.Register;
using GMSoft.Application.Features.Sessions.AddStock;
using GMSoft.Application.Features.Sessions.Close;
using GMSoft.Application.Features.Sessions.Open;
using GMSoft.Application.Features.VehicleLoads.Register;
using GMSoft.Data.Context;
using GMSoft.Data.Identity;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GMSoft.IntegrationTests;

public sealed class Scenario(PostgresFixture fixture)
{
    public Guid ZoneId { get; } = Guid.NewGuid();
    public Guid VehicleId { get; } = Guid.NewGuid();
    public Guid DriverId { get; } = Guid.NewGuid();
    public Guid UserId { get; } = Guid.NewGuid();
    public Guid AdminId { get; } = Guid.NewGuid();
    public Guid CustomerId { get; } = Guid.NewGuid();
    public Guid ProductId { get; } = Guid.NewGuid();
    public Guid NoneId { get; } = Guid.NewGuid();
    public Guid UnitId { get; } = Guid.NewGuid();
    public Guid SessionId { get; set; }

    public IServiceScope Scope(bool admin = false, SqlReadBarrier? barrier = null)
    {
        var scope = fixture.Services.CreateScope();
        var user = scope.ServiceProvider.GetRequiredService<TestUser>();
        user.Admin = admin;
        user.UserId = admin ? AdminId : UserId;
        user.DriverId = admin ? null : DriverId;
        scope.ServiceProvider.GetRequiredService<ScopeInstrumentation>().ReadBarrier = barrier;
        return scope;
    }

    public async Task<T> Send<T>(IRequest<T> request, bool admin = false, SqlReadBarrier? barrier = null)
    {
        using var scope = Scope(admin, barrier);
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request);
    }

    public async Task Send(IRequest request)
    {
        using var scope = Scope();
        await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request);
    }

    public async Task<T> Read<T>(Func<AppDbContext, Task<T>> read)
    {
        using var scope = Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        return await read(db);
    }

    public async Task Write(Func<AppDbContext, Task> write)
    {
        using var scope = Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await write(db);
        await db.SaveChangesAsync();
    }

    public Customer Customer(int order = 7, int[]? days = null) => new()
    {
        Id = Guid.NewGuid(), ContactName = "Cliente " + order, Phone = "123", Address = "Calle 1",
        ZoneId = ZoneId, VehicleId = VehicleId, VisitDays = days ?? [1], RouteOrder = order
    };

    public async Task<Scenario> Seed(bool open = true, int stock = 20)
    {
        await Write(db =>
        {
            db.Users.AddRange(new ApplicationUser { Id = UserId, UserName = UserId.ToString("N"), NormalizedUserName = UserId.ToString("N") },
                new ApplicationUser { Id = AdminId, UserName = AdminId.ToString("N"), NormalizedUserName = AdminId.ToString("N") });
            db.Zones.Add(new Zone { Id = ZoneId, Name = "Zona " + ZoneId });
            db.Vehicles.Add(new Vehicle { Id = VehicleId, Name = "Camion", LicensePlate = VehicleId.ToString("N")[..12],
                Type = VehicleType.Truck, CurrentKilometers = 100 });
            db.Drivers.Add(new Driver { Id = DriverId, FirstName = "Juan", LastName = "Prueba", DocumentNumber = DriverId.ToString("N"),
                ApplicationUserId = UserId, VehicleId = VehicleId });
            db.Products.AddRange(new Product { Id = ProductId, Detail = "Bidon", SalePrice = 100, Tracking = ContainerTracking.ByBalance, IsPublished = true },
                new Product { Id = NoneId, Detail = "Agua", SalePrice = 50, Tracking = ContainerTracking.None, IsPublished = true },
                new Product { Id = UnitId, Detail = "Dispenser", SalePrice = 200, Tracking = ContainerTracking.ByUnit, IsPublished = true });
            var customer = Customer(); customer.Id = CustomerId; db.Customers.Add(customer);
            return Task.CompletedTask;
        });
        if (open)
        {
            await Load(stock);
            SessionId = await Send(new OpenSessionCommand(ZoneId, 100));
        }
        return this;
    }

    public Task<RegisterVehicleLoadResult> Load(int stock = 20) => Send(new RegisterVehicleLoadCommand(VehicleId,
        [new(ProductId, stock), new(NoneId, stock), new(UnitId, stock)], [1]), true);

    public RegisterDeliveryCommand Sale(int sold = 3, int returned = 0, bool paid = false) => new(CustomerId, null,
        DeliveryType.Sale, [new(ProductId, sold)], [], returned > 0 ? [new(ProductId, returned)] : [],
        paid ? new(null, PaymentMethod.Cash) : null, null, Guid.NewGuid());

    public AddSessionStockCommand Restock() => new(SessionId, [new(ProductId, 3), new(NoneId, 2)], Guid.NewGuid(), " En ruta ");

    public CloseSessionCommand Close(int full = 20, int empty = 0) => new(SessionId, 110,
        [.. (full > 0 ? new[] { new CloseSessionReturnLine(ProductId, ContainerState.Full, full) } : []),
         .. (empty > 0 ? new[] { new CloseSessionReturnLine(ProductId, ContainerState.Empty, empty) } : []),
         new(NoneId, ContainerState.Full, 20), new(UnitId, ContainerState.Full, 20)]);

    public Task<int> Stock(ContainerState state = ContainerState.Full, Guid? product = null) => Read(db =>
        db.SessionStockMovements.Where(m => m.DeliverySessionId == SessionId && m.ProductId == (product ?? ProductId) && m.State == state)
            .SumAsync(m => m.Quantity));

    public Task<int> Sales() => Read(db => db.Deliveries.CountAsync(d => d.DeliverySessionId == SessionId));
    public Task<int> Balance() => Read(db => db.CustomerContainerBalances.Where(b => b.CustomerId == CustomerId && b.ProductId == ProductId)
        .SumAsync(b => b.Quantity));
}
