using System.Reflection;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Sessions.Open;
using GMSoft.Application.Features.Sessions.Close;
using GMSoft.Application.Features.VehicleLoads.Register;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;

namespace GMSoft.Application.Tests.Features.Sessions;

public class DepartureWorkflowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Departure_requires_admin_load_and_transfers_full_products(bool loaded)
    {
        var driverId = Guid.NewGuid();
        var vehicle = new Vehicle { Id = Guid.NewGuid(), CurrentKilometers = 100 };
        var driver = new Driver { Id = driverId, VehicleId = vehicle.Id, IsActive = true };
        var zoneId = Guid.NewGuid();
        var load = new VehicleLoad { VehicleId = vehicle.Id, ProductId = Guid.NewGuid(), Quantity = 20, RouteDays = [1, 2] };
        IReadOnlyList<VehicleLoad> pending = loaded ? [load] : [];
        DeliverySession? saved = null;
        var sessions = Stub<ISessionRepository>((name, args) => name switch
        {
            "GetOpenByDriverAsync" => Task.FromResult<DeliverySession?>(null),
            "HasOpenSessionForVehicleAsync" => Task.FromResult(false),
            "AddAsync" => Capture((DeliverySession)args![0]!),
            _ => throw new InvalidOperationException(name)
        });
        Task Capture(DeliverySession session) { saved = session; return Task.CompletedTask; }
        var handler = new OpenSessionCommandHandler(sessions,
            Stub<IDriverRepository>((_, _) => Task.FromResult<Driver?>(driver)),
            Stub<IVehicleRepository>((name, _) => name == "Update" ? null : Task.FromResult<Vehicle?>(vehicle)),
            Stub<IZoneRepository>((_, _) => Task.FromResult(true)),
            Stub<IVehicleLoadRepository>((name, args) => name switch
            {
                "GetPendingAsync" when (Guid)args![0]! == vehicle.Id => Task.FromResult(pending),
                "Update" => null,
                _ => throw new InvalidOperationException(name)
            }),
            Stub<ICurrentUserService>((name, _) => name == "get_DriverId" ? driverId : null),
            Stub<IUnitOfWork>((name, args) => name switch
            {
                "ExecuteInTransactionAsync" => ((Func<Task>)args![0]!)(),
                "SaveChangesAsync" => Task.FromResult(1),
                _ => throw new InvalidOperationException(name)
            }));

        if (!loaded)
        {
            await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(new(zoneId, 101), default));
            Assert.Null(saved);
            Assert.Equal(100, vehicle.CurrentKilometers);
            return;
        }

        await handler.Handle(new(zoneId, 101), default);
        Assert.NotNull(saved);
        Assert.Equal(zoneId, saved.ZoneId);
        Assert.Equal(new[] { 1, 2 }, saved.RouteDays);
        Assert.NotSame(load.RouteDays, saved.RouteDays);
        Assert.Equal(driverId, saved.DriverId);
        Assert.Same(saved, load.ConsumedBySession);
        var stock = Assert.Single(saved.StockMovements);
        Assert.Equal(ContainerState.Full, stock.State);
        Assert.Equal(20, stock.Quantity);
        Assert.Equal(101, vehicle.CurrentKilometers);
    }

    [Fact]
    public async Task Driver_cannot_prepare_departure_load()
    {
        var user = Stub<ICurrentUserService>((_, _) => false);
        var handler = new RegisterVehicleLoadCommandHandler(null!, null!, null!, null!, user, null!);
        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(new(Guid.NewGuid(), []), default));
    }

    [Fact]
    public void Reception_accepts_full_and_empty_units_of_same_product()
    {
        var product = Guid.NewGuid();
        var command = new CloseSessionCommand(Guid.NewGuid(), 150,
            [new(product, ContainerState.Full, 5), new(product, ContainerState.Empty, 15)]);
        Assert.True(new CloseSessionCommandValidator().Validate(command).IsValid);
    }

    private static T Stub<T>(Func<string, object?[]?, object?> invoke) where T : class
    {
        var proxy = DispatchProxy.Create<T, WorkflowProxy>();
        ((WorkflowProxy)(object)proxy).Handler = invoke;
        return proxy;
    }

    public class WorkflowProxy : DispatchProxy
    {
        public Func<string, object?[]?, object?> Handler { get; set; } = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
            => Handler(method!.Name, args);
    }
}
