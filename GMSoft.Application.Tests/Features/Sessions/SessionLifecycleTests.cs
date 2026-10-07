using System.Reflection;
using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Sessions.Close;
using GMSoft.Application.Features.Sessions.Common;
using GMSoft.Application.Features.Sessions.GetCurrent;
using GMSoft.Application.Features.Sessions.GetStatus;
using GMSoft.Application.Features.Sessions.Open;
using GMSoft.Application.Features.Sessions.KeepAlive;
using GMSoft.Application.Common.Models;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;

namespace GMSoft.Application.Tests.Features.Sessions;

public class SessionLifecycleTests
{
    [Theory]
    [InlineData(SessionStatus.Open)]
    [InlineData(SessionStatus.Closed)]
    public async Task Access_renews_only_while_own_departure_is_open(SessionStatus status)
    {
        var session = Departure(); session.Status = status;
        var accountId = Guid.NewGuid();
        var driver = new Driver { Id = session.DriverId, ApplicationUserId = accountId, IsActive = true };
        AuthUserData? issued = null;
        var tokens = Stub<IJwtTokenService>((_, args) => { issued = (AuthUserData)args![0]!; return "renewed-token"; });
        var handler = new KeepSessionAliveCommandHandler(
            Stub<ISessionRepository>((_, _) => Task.FromResult<DeliverySession?>(session)),
            Stub<IDriverRepository>((_, _) => Task.FromResult<Driver?>(driver)), HeartbeatUser(driver.Id, accountId), tokens);
        var result = await handler.Handle(new(session.Id), default);
        if (status == SessionStatus.Closed) { Assert.Null(issued); Assert.Null(result.Token); }
        else
        {
            Assert.NotNull(issued);
            Assert.Equal(accountId, issued.UserId);
            Assert.Equal(driver.Id, issued.DriverId);
            Assert.Equal(new[] { AppRoles.Driver }, issued.Roles);
            Assert.Equal("renewed-token", result.Token);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Disabled_or_unlinked_driver_cannot_renew_access(bool active, bool otherAccount)
    {
        var session = Departure();
        var accountId = Guid.NewGuid();
        var driver = new Driver { Id = session.DriverId, IsActive = active, ApplicationUserId = otherAccount ? Guid.NewGuid() : accountId };
        var handler = new KeepSessionAliveCommandHandler(
            Stub<ISessionRepository>((_, _) => Task.FromResult<DeliverySession?>(session)),
            Stub<IDriverRepository>((_, _) => Task.FromResult<Driver?>(driver)), HeartbeatUser(driver.Id, accountId), null!);
        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(new(session.Id), default));
    }

    private static ICurrentUserService HeartbeatUser(Guid driverId, Guid accountId) => Stub<ICurrentUserService>((name, args) => name switch
    {
        "get_DriverId" => driverId,
        "get_UserId" => accountId,
        "get_UserName" => "chofer.prueba",
        "IsInRole" => (string)args![0]! == AppRoles.Driver,
        _ => null
    });

    [Theory]
    [InlineData(SessionStatus.Open)]
    [InlineData(SessionStatus.Closed)]
    public async Task Driver_can_check_own_departure_before_and_after_reception(SessionStatus status)
    {
        var session = Departure(); session.Status = status;
        session.ClosedAt = status == SessionStatus.Closed ? DateTime.UtcNow : null;
        var handler = new GetSessionStatusQueryHandler(
            Stub<ISessionRepository>((name, _) => name == "GetByIdAsync" ? Task.FromResult<DeliverySession?>(session) : throw new InvalidOperationException(name)),
            User(session.DriverId));
        var result = await handler.Handle(new(session.Id), default);
        Assert.Equal(session.Status, result.Status);
        Assert.Equal(session.VehicleId, result.VehicleId);
        Assert.Equal(session.ClosedAt, result.ClosedAt);
    }

    [Fact]
    public async Task Driver_cannot_monitor_another_drivers_departure()
    {
        var session = Departure();
        var handler = new GetSessionStatusQueryHandler(Stub<ISessionRepository>((_, _) => Task.FromResult<DeliverySession?>(session)), User(Guid.NewGuid()));
        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(new(session.Id), default));
    }

    [Fact]
    public async Task Driver_cannot_close_departure_or_record_returns()
    {
        var session = Departure();
        var handler = new CloseSessionCommandHandler(Stub<ISessionRepository>((_, _) => Task.FromResult<DeliverySession?>(session)), null!, User(session.DriverId), null!);
        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(new(session.Id, 110, [new(Guid.NewGuid(), ContainerState.Empty, 3)]), default));
        Assert.Equal(SessionStatus.Open, session.Status);
        Assert.Null(session.ClosedAt);
        Assert.Empty(session.StockMovements);
    }

    [Fact]
    public async Task Existing_departure_blocks_new_open_until_admin_reception()
    {
        var session = Departure();
        var driver = new Driver { Id = session.DriverId, IsActive = true, VehicleId = session.VehicleId };
        var handler = new OpenSessionCommandHandler(
            Stub<ISessionRepository>((_, _) => Task.FromResult<DeliverySession?>(session)),
            Stub<IDriverRepository>((_, _) => Task.FromResult<Driver?>(driver)), null!, null!, null!, User(driver.Id), null!);
        var error = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(new(Guid.NewGuid(), 100), default));
        Assert.Contains("recepción", error.Message);
        Assert.Equal(SessionStatus.Open, session.Status);
    }

    [Theory]
    [InlineData(SessionStatus.Open)]
    [InlineData(SessionStatus.Closed)]
    public async Task Current_departure_resumes_previous_days_but_excludes_a_concurrent_reception(SessionStatus status)
    {
        var session = Departure(); session.Status = status;
        var handler = new GetCurrentSessionQueryHandler(Stub<ISessionRepository>((name, _) => name switch
        {
            "GetOpenByDriverAsync" => Task.FromResult<DeliverySession?>(session),
            "GetWithDetailsAsync" => Task.FromResult<DeliverySession?>(session),
            "GetStockBalanceAsync" => Task.FromResult<IReadOnlyList<SessionStockLineDto>>([]),
            _ => throw new InvalidOperationException(name)
        }), User(session.DriverId));
        var result = await handler.Handle(new(), default);
        if (status == SessionStatus.Closed) Assert.Null(result);
        else { Assert.NotNull(result); Assert.Equal(session.Id, result.Id); Assert.Equal(session.OpenedAt, result.OpenedAt); }
    }

    [Fact]
    public async Task Admin_reception_closes_original_truck_with_full_and_empty_returns_once()
    {
        var session = Departure();
        session.Driver = new Driver { Id = session.DriverId, VehicleId = Guid.NewGuid() };
        var vehicle = new Vehicle { Id = session.VehicleId, CurrentKilometers = 100 };
        var productId = Guid.NewGuid();
        var sessions = Stub<ISessionRepository>((name, _) => name switch
        {
            "GetByIdAsync" => Task.FromResult<DeliverySession?>(session),
            "Update" => null,
            "GetStockBalanceAsync" => Task.FromResult<IReadOnlyList<SessionStockLineDto>>([]),
            _ => throw new InvalidOperationException(name)
        });
        var vehicles = Stub<IVehicleRepository>((name, args) => name switch
        {
            "GetByIdAsync" when (Guid)args![0]! == session.VehicleId => Task.FromResult<Vehicle?>(vehicle),
            "Update" => null,
            _ => throw new InvalidOperationException(name)
        });
        var unit = Stub<IUnitOfWork>((name, args) => name switch
        {
            "ExecuteInTransactionAsync" => ((Func<Task>)args![0]!)(),
            "SaveChangesAsync" => Task.FromResult(1),
            _ => throw new InvalidOperationException(name)
        });
        var handler = new CloseSessionCommandHandler(sessions, vehicles, User(null, true), unit);
        var request = new CloseSessionCommand(session.Id, 120, [new(productId, ContainerState.Full, 2), new(productId, ContainerState.Empty, 3)]);
        await handler.Handle(request, default);
        Assert.Equal(SessionStatus.Closed, session.Status);
        Assert.NotNull(session.ClosedAt);
        Assert.Equal(120, vehicle.CurrentKilometers);
        Assert.Collection(session.StockMovements,
            movement => { Assert.Equal(ContainerState.Full, movement.State); Assert.Equal(-2, movement.Quantity); },
            movement => { Assert.Equal(ContainerState.Empty, movement.State); Assert.Equal(-3, movement.Quantity); });
        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(request, default));
        Assert.Equal(2, session.StockMovements.Count);
    }

    private static DeliverySession Departure() => new()
    {
        Id = Guid.NewGuid(), DriverId = Guid.NewGuid(), VehicleId = Guid.NewGuid(),
        KilometersAtOpen = 100, OpenedAt = DateTime.UtcNow.AddDays(-2), Status = SessionStatus.Open
    };
    private static ICurrentUserService User(Guid? driverId, bool admin = false) => Stub<ICurrentUserService>((name, _) => name switch
    {
        "get_DriverId" => driverId,
        "IsInRole" => admin,
        "get_UserId" => Guid.NewGuid(),
        _ => null
    });
    private static T Stub<T>(Func<string, object?[]?, object?> invoke) where T : class
    {
        var proxy = DispatchProxy.Create<T, LifecycleProxy>();
        ((LifecycleProxy)(object)proxy).Handler = invoke;
        return proxy;
    }
    public class LifecycleProxy : DispatchProxy
    {
        public Func<string, object?[]?, object?> Handler { get; set; } = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!.Name, args);
    }
}
