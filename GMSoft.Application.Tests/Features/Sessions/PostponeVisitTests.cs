using System.Reflection;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Sessions.Postpone;
using GMSoft.Domain.Entities;

namespace GMSoft.Application.Tests.Features.Sessions;

public class PostponeVisitTests
{
    [Fact]
    public async Task Postpone_marks_current_session_once_without_changing_customer()
    {
        var fixture = new Fixture();
        await fixture.Handler.Handle(new(fixture.Customer.Id), default);
        await fixture.Handler.Handle(new(fixture.Customer.Id), default);
        Assert.Equal(new[] { fixture.Customer.Id }, fixture.Session.DeferredCustomerIds);
        Assert.Equal(1, fixture.Saves);
        Assert.Equal(4, fixture.Customer.RouteOrder);
        Assert.Equal(new[] { 1, 5 }, fixture.Customer.VisitDays);
        Assert.Empty(fixture.Session.Deliveries);
        Assert.Empty(fixture.Session.StockMovements);
    }

    [Fact]
    public async Task Postpone_preserves_other_pending_customers()
    {
        var fixture = new Fixture();
        var other = Guid.NewGuid();
        fixture.Session.DeferredCustomerIds = [other];
        await fixture.Handler.Handle(new(fixture.Customer.Id), default);
        Assert.Equal(new[] { other, fixture.Customer.Id }, fixture.Session.DeferredCustomerIds);
    }

    [Fact]
    public async Task No_driver_cannot_postpone()
    {
        var fixture = new Fixture { DriverId = null };
        await Assert.ThrowsAsync<ForbiddenException>(() => fixture.Handler.Handle(new(fixture.Customer.Id), default));
        Assert.Equal(0, fixture.Saves);
    }

    [Fact]
    public async Task Closed_or_missing_session_cannot_postpone()
    {
        var fixture = new Fixture { Open = false };
        await Assert.ThrowsAsync<ConflictException>(() => fixture.Handler.Handle(new(fixture.Customer.Id), default));
        Assert.Equal(0, fixture.Saves);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Inactive_or_other_zone_customer_cannot_postpone(bool inactive)
    {
        var fixture = new Fixture();
        if (inactive) fixture.Customer.IsActive = false;
        else fixture.Customer.ZoneId = Guid.NewGuid();
        await Assert.ThrowsAsync<BadRequestException>(() => fixture.Handler.Handle(new(fixture.Customer.Id), default));
        Assert.Null(fixture.Session.DeferredCustomerIds);
        Assert.Equal(0, fixture.Saves);
    }

    private sealed class Fixture
    {
        public Guid? DriverId = Guid.NewGuid();
        public bool Open = true;
        public int Saves;
        public DeliverySession Session = new() { Id = Guid.NewGuid(), ZoneId = Guid.NewGuid() };
        public Customer Customer = new() { Id = Guid.NewGuid(), IsActive = true, RouteOrder = 4, VisitDays = [1, 5] };
        public PostponeCustomerVisitCommandHandler Handler { get; }
        public Fixture()
        {
            Customer.ZoneId = Session.ZoneId;
            Handler = new(
                Stub<ISessionRepository>((name, args) => name switch {
                    "GetOpenByDriverAsync" when (Guid)args![0]! == DriverId => Task.FromResult<DeliverySession?>(Open ? Session : null),
                    "Update" => null,
                    _ => throw new InvalidOperationException(name)
                }),
                Stub<ICustomerRepository>((_, _) => Task.FromResult<Customer?>(Customer)),
                Stub<ICurrentUserService>((_, _) => DriverId),
                Stub<IUnitOfWork>((_, _) => Task.FromResult(++Saves)));
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
