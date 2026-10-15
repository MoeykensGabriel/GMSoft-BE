using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Customers.Account;
using GMSoft.Application.Features.Customers.GetById;
using GMSoft.Application.Features.Customers.GetList;
using GMSoft.Application.Features.Customers.Removal;
using GMSoft.Application.Features.Customers.Update;
using GMSoft.Application.Features.Sessions.Unvisited;
using GMSoft.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class CustomerRemovalTests(PostgresFixture fixture)
{
    private static RequestRemoval Request(Scenario f, bool missing = false) =>
        new(f.CustomerId, Guid.NewGuid(), RemovalReason.Moved, "Otra ciudad", missing);

    [Fact]
    public async Task No_containers_removes_from_sheet_unvisited_and_route_and_blocks_sale()
    {
        var f = await new Scenario(fixture).Seed();
        var result = await f.Send(Request(f));
        Assert.Equal(RemovalStatus.Pending, result.Status); Assert.Empty(result.Containers);
        Assert.Equal(f.UserId, result.RequestedByUserId); Assert.Equal(f.DriverId, result.DriverId);
        Assert.Empty((await f.Send(new GetCustomersQuery())).Items);
        Assert.Empty((await f.Send(new GetUnvisitedCustomersQuery(f.SessionId))).Items);
        using var scope = f.Scope(true);
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<IRoutePlanningRepository>().GetRouteAsync(f.VehicleId, f.ZoneId, 1, default));
        var error = await Assert.ThrowsAsync<ConflictException>(() => f.Send(f.Sale()));
        Assert.Equal("El cliente fue dado de baja; lo confirma la oficina", error.Message);
        Assert.Equal(7, await f.Read(db => db.Customers.Where(c => c.Id == f.CustomerId).Select(c => c.RouteOrder).SingleAsync()));
    }

    [Fact]
    public async Task Admin_sees_the_pending_removal_on_the_customer_until_it_is_resolved()
    {
        var f = await new Scenario(fixture).Seed();
        var request = await f.Send(Request(f));
        var listed = Assert.Single((await f.Send(new GetCustomersQuery(VehicleId: f.VehicleId), true)).Items);
        Assert.True(listed.RemovalPending);
        Assert.True((await f.Send(new GetCustomerByIdQuery(f.CustomerId), true)).RemovalPending);
        await f.Send(new ResolveRemoval(request.Id, false), true);
        Assert.False((await f.Send(new GetCustomerByIdQuery(f.CustomerId), true)).RemovalPending);
    }

    [Fact]
    public async Task Containers_block_and_override_keeps_frozen_containers_and_debt()
    {
        var f = await new Scenario(fixture).Seed();
        await f.Send(f.Sale(3));
        var error = await Assert.ThrowsAsync<ContainersOutstandingException>(() => f.Send(Request(f)));
        Assert.Equal(3, Assert.Single(error.Containers).Quantity);
        Assert.Equal(f.ProductId, error.Containers[0].ProductId);
        var result = await f.Send(Request(f, true));
        Assert.Equal(300, result.Debt); Assert.Equal(3, Assert.Single(result.Containers).Quantity);
        Assert.Equal(3, await f.Balance());
        await f.Write(async db => { var p = await db.Products.SingleAsync(p => p.Id == f.ProductId); p.Detail = "Nuevo nombre"; });
        var page = await f.Send(new ListRemovals(), true);
        Assert.Equal("Bidon", page.Items.Single(r => r.Id == result.Id).Containers[0].ProductDetail);
    }

    [Fact]
    public async Task Unit_tracked_containers_also_block()
    {
        var f = await new Scenario(fixture).Seed();
        await f.Write(db => { db.ContainerUnits.Add(new() { ProductId = f.UnitId, CurrentCustomerId = f.CustomerId, SerialNumber = Guid.NewGuid().ToString() }); return Task.CompletedTask; });
        var error = await Assert.ThrowsAsync<ContainersOutstandingException>(() => f.Send(Request(f)));
        Assert.Equal(f.UnitId, Assert.Single(error.Containers).ProductId);
        Assert.Single((await f.Send(Request(f, true))).Containers);
    }

    [Fact]
    public async Task Debt_without_containers_does_not_block()
    {
        var f = await new Scenario(fixture).Seed();
        await f.Write(db => { db.Deliveries.Add(new() { CustomerId = f.CustomerId, DeliverySessionId = f.SessionId, DeliveredAt = DateTime.UtcNow, Total = 123 }); return Task.CompletedTask; });
        Assert.Equal(123, (await f.Send(Request(f))).Debt);
    }

    [Fact]
    public async Task Foreign_driver_is_forbidden()
    {
        var f = await new Scenario(fixture).Seed(); var other = await new Scenario(fixture).Seed();
        await Assert.ThrowsAsync<ForbiddenException>(() => other.Send(Request(f)));
    }

    [Fact]
    public async Task No_open_session_is_conflict()
    {
        var f = await new Scenario(fixture).Seed(false);
        await Assert.ThrowsAsync<ConflictException>(() => f.Send(Request(f)));
    }

    [Fact]
    public async Task Retry_after_resolution_returns_original_and_changed_payload_conflicts()
    {
        var f = await new Scenario(fixture).Seed(); var command = Request(f);
        var result = await f.Send(command);
        await f.Send(new ResolveRemoval(result.Id, true), true);
        Assert.Equal(result.Id, (await f.Send(command)).Id);
        await Assert.ThrowsAsync<ConflictException>(() => f.Send(command with { Reason = RemovalReason.Deceased }));
        await Assert.ThrowsAsync<ConflictException>(() => f.Send(new ResolveRemoval(result.Id, false), true));
        Assert.Equal(result.Id, (await f.Send(new ResolveRemoval(result.Id, true), true)).Id);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Resolution_preserves_position_and_history_and_admin_can_reactivate(bool confirm)
    {
        var f = await new Scenario(fixture).Seed(); var request = await f.Send(Request(f));
        var resolved = await f.Send(new ResolveRemoval(request.Id, confirm), true);
        Assert.Equal(f.AdminId, resolved.ResolvedByUserId); Assert.NotNull(resolved.ResolvedAt);
        var customer = await f.Read(db => db.Customers.SingleAsync(c => c.Id == f.CustomerId));
        Assert.Equal(!confirm, customer.IsActive); Assert.Equal(7, customer.RouteOrder);
        var account = await f.Send(new GetCustomerAccountQuery(f.CustomerId), true);
        Assert.Equal(2, account.StatementTotalCount);
        Assert.Contains(account.Statement, r => r.Kind == (confirm ? "RemovalConfirmed" : "RemovalRejected"));
        Assert.All(account.Statement, r => { Assert.Equal(0, r.Charge); Assert.Equal(0, r.Paid); });
        if (confirm) await f.Send(new UpdateCustomerCommand(f.CustomerId, null, customer.ContactName, customer.Phone,
            customer.Address, null, f.ZoneId, null, null, true, [1], f.VehicleId), true);
        Assert.Single((await f.Send(new GetCustomersQuery())).Items);
        Assert.Equal(7, await f.Read(db => db.Customers.Where(c => c.Id == f.CustomerId).Select(c => c.RouteOrder).SingleAsync()));
        Assert.Equal(resolved.Status, await f.Read(db => db.Set<CustomerRemovalRequest>().Where(r => r.Id == request.Id).Select(r => r.Status).SingleAsync()));
        await f.Send(f.Sale());
    }

    [Fact]
    public async Task Admin_list_is_paged_and_pending_count_does_not_depend_on_status_filter()
    {
        var f = await new Scenario(fixture).Seed(); var request = await f.Send(Request(f));
        var before = await f.Send(new ListRemovals(PageSize: 1), true);
        Assert.True(before.PendingCount > 0); Assert.Single(before.Items);
        await f.Send(new ResolveRemoval(request.Id, false), true);
        var after = await f.Send(new ListRemovals(RemovalStatus.Rejected, PageSize: 1), true);
        Assert.Equal(before.PendingCount - 1, after.PendingCount); Assert.Single(after.Items);
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Send(new ListRemovals()));
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Send(new ResolveRemoval(request.Id, true)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Concurrent_requests_do_not_duplicate_pending(bool sameKey)
    {
        var f = await new Scenario(fixture).Seed(); var command = Request(f);
        async Task<RemovalDto?> Attempt(RequestRemoval r)
        {
            try { return await f.Send(r); } catch (ConflictException) { return null; }
        }
        var results = await Task.WhenAll(Attempt(command), Attempt(sameKey ? command : command with { ClientRequestId = Guid.NewGuid() }));
        Assert.Equal(sameKey ? 2 : 1, results.Count(r => r != null));
        Assert.Equal(1, await f.Read(db => db.Set<CustomerRemovalRequest>().CountAsync(r => r.CustomerId == f.CustomerId)));
        if (sameKey) Assert.Equal(results[0]!.Id, results[1]!.Id);
    }
}
