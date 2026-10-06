using System.Reflection;
using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Customers.Account;
using GMSoft.Application.Features.Customers.Common;
using GMSoft.Application.Features.Customers.GetById;
using GMSoft.Application.Features.Customers.GetList;
using GMSoft.Domain.Entities;

namespace GMSoft.Application.Tests.Features.Customers;

public class CustomerRouteAccessTests
{
    [Theory]
    [InlineData("vehicle")]
    [InlineData("unassigned")]
    [InlineData("zone")]
    [InlineData("day")]
    [InlineData("missingDays")]
    [InlineData("inactive")]
    public void Driver_cannot_operate_on_customers_outside_departure(string mismatch)
    {
        var session = Session();
        var customer = Customer(session);
        switch (mismatch)
        {
            case "vehicle": customer.VehicleId = Guid.NewGuid(); break;
            case "unassigned": customer.VehicleId = null; break;
            case "zone": customer.ZoneId = Guid.NewGuid(); break;
            case "day": customer.VisitDays = [7]; break;
            case "missingDays": customer.VisitDays = null; break;
            case "inactive": customer.IsActive = false; break;
        }
        Assert.Throws<BadRequestException>(() => CustomerRouteAccess.EnsureMatchesSession(customer, session));
    }

    [Fact]
    public void Any_selected_route_day_matches_and_does_not_depend_on_today()
    {
        var session = Session();
        var customer = Customer(session);
        customer.VisitDays = [5, 7];
        CustomerRouteAccess.EnsureMatchesSession(customer, session);
        session.RouteDays = null;
        session.OpenedAt = new DateTime(2026, 10, 6, 2, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new[] { 1 }, CustomerRouteAccess.Days(session));
    }

    [Fact]
    public async Task List_uses_departure_vehicle_zone_and_days_even_if_request_is_manipulated()
    {
        var session = Session();
        var own = Customer(session);
        object?[]? received = null;
        var customers = Stub<ICustomerRepository>((name, args) => name switch
        {
            "GetPagedAsync" => Page(args),
            "GetLastPurchaseDatesAsync" => Task.FromResult<IReadOnlyDictionary<Guid, DateTime>>(new Dictionary<Guid, DateTime>()),
            _ => throw new InvalidOperationException(name)
        });
        Task<(IReadOnlyList<Customer>, int)> Page(object?[]? args)
        {
            received = args;
            return Task.FromResult<(IReadOnlyList<Customer>, int)>(([own], 1));
        }
        var handler = new GetCustomersQueryHandler(customers, new(15, 30), Access(session));
        var result = await handler.Handle(new(ZoneId: Guid.NewGuid(), VehicleId: Guid.NewGuid(),
            VisitDays: [7], OnlyActive: false, Search: "Ana"), default);
        Assert.NotNull(received);
        Assert.Equal("Ana", received[2]);
        Assert.Equal(session.ZoneId, received[3]);
        Assert.Equal(true, received[4]);
        Assert.Equal(session.RouteDays, (int[])received[7]!);
        Assert.Equal(session.VehicleId, received[8]);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(own.Id, Assert.Single(result.Items).Id);
    }

    [Fact]
    public async Task Detail_account_and_prices_reject_other_truck_before_returning_data()
    {
        var session = Session();
        var other = Customer(session);
        other.VehicleId = Guid.NewGuid();
        var customers = Stub<ICustomerRepository>((_, _) => Task.FromResult<Customer?>(other));
        var access = Access(session);
        await Assert.ThrowsAsync<BadRequestException>(() =>
            new GetCustomerByIdQueryHandler(customers, new(15, 30), access).Handle(new(other.Id), default));
        await Assert.ThrowsAsync<BadRequestException>(() =>
            new GetCustomerAccountQueryHandler(customers, Unexpected<IContainerBalanceRepository>(),
                Unexpected<IContainerUnitRepository>(), access).Handle(new(other.Id, 50), default));
        await Assert.ThrowsAsync<BadRequestException>(() =>
            new GetCustomerPricesQueryHandler(customers, Unexpected<ICustomerPriceRepository>(), access)
                .Handle(new(other.Id), default));
    }

    [Fact]
    public async Task Admin_can_read_unassigned_customers_without_open_session()
    {
        var access = new CustomerRouteAccess(
            Stub<ICurrentUserService>((name, args) => name == "IsInRole" && (string)args![0]! == AppRoles.Admin),
            Unexpected<ISessionRepository>());
        Assert.Null(await access.GetDriverSessionAsync(default));
        await access.EnsureCanReadAsync(new Customer(), default);
    }

    [Fact]
    public async Task Driver_without_open_session_cannot_read_customers()
        => await Assert.ThrowsAsync<ConflictException>(() => Access(null).GetDriverSessionAsync(default));

    private static DeliverySession Session() => new()
    {
        Id = Guid.NewGuid(), VehicleId = Guid.NewGuid(), ZoneId = Guid.NewGuid(), RouteDays = [1, 5]
    };
    private static Customer Customer(DeliverySession session) => new()
    {
        Id = Guid.NewGuid(), VehicleId = session.VehicleId, ZoneId = session.ZoneId, VisitDays = [1],
        IsActive = true, CreatedAt = DateTime.UtcNow
    };
    private static CustomerRouteAccess Access(DeliverySession? session) => new(
        Stub<ICurrentUserService>((name, _) => name switch
        {
            "IsInRole" => false, "get_DriverId" => (Guid?)Guid.NewGuid(),
            _ => throw new InvalidOperationException(name)
        }), Stub<ISessionRepository>((_, _) => Task.FromResult(session)));
    private static T Unexpected<T>() where T : class => Stub<T>((name, _) => throw new InvalidOperationException(name));
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
