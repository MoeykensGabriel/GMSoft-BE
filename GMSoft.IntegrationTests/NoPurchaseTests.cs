using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Features.Customers.Account;
using GMSoft.Application.Features.Customers.GetById;
using GMSoft.Application.Features.Customers.GetList;
using GMSoft.Application.Features.Deliveries.Register;
using GMSoft.Application.Features.Sessions.DetailedSettlement;
using GMSoft.Application.Features.Sessions.GetDeliveries;
using GMSoft.Application.Features.Sessions.Unvisited;
using GMSoft.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class NoPurchaseTests(PostgresFixture fixture)
{
    private static RegisterDeliveryCommand Visit(Scenario f) => new(f.CustomerId, null,
        DeliveryType.NoPurchase, [], [], [], null, null, Guid.NewGuid(), NoPurchaseReason.NotHome);

    [Fact]
    public async Task Visit_and_correction_are_idempotent_without_side_effects_then_sale_is_allowed()
    {
        var f = await new Scenario(fixture).Seed();
        var command = Visit(f);
        var first = await f.Send(command);
        Assert.Equal(first, await f.Send(command));
        var correction = command with { ClientRequestId = Guid.NewGuid(), NoPurchaseReason = NoPurchaseReason.Other, Notes = " Mañana " };
        Assert.Equal(first, await f.Send(correction));
        Assert.Equal(first, await f.Send(command));
        Assert.Equal(first, await f.Send(correction));
        Assert.Equal(1, await f.Sales()); Assert.Equal(20, await f.Stock()); Assert.Equal(0, await f.Balance());
        Assert.False(await f.Read(db => db.Payments.AnyAsync(p => p.DeliverySessionId == f.SessionId)));
        var account = await f.Send(new GetCustomerAccountQuery(f.CustomerId), true);
        Assert.Empty(account.Movements); Assert.Equal(0, account.Balance); Assert.Null(account.LastPurchaseAt);
        Assert.Equal(NoPurchaseReason.Other, account.LastNoPurchase!.Reason);
        Assert.Equal("Mañana", account.LastNoPurchase.Notes);
        var detail = await f.Send(new GetCustomerByIdQuery(f.CustomerId), true);
        Assert.Equal(account.LastNoPurchase, detail.LastNoPurchase);
        Assert.Equal(detail.LastNoPurchase!.Date, detail.LastVisitAt);
        Assert.Equal(detail.LastNoPurchase, Assert.Single((await f.Send(new GetCustomersQuery(ZoneId: f.ZoneId), true)).Items).LastNoPurchase);
        var settlement = Assert.Single(await f.Send(new GetSessionDetailedSettlementQuery(f.SessionId), true));
        Assert.Empty(settlement.Lines); Assert.Equal(account.LastNoPurchase, Assert.Single(settlement.NoPurchases));
        await f.Send(f.Sale());
        Assert.Null((await f.Send(new GetCustomerByIdQuery(f.CustomerId), true)).LastNoPurchase);
        Assert.Null((await f.Send(new GetCustomerAccountQuery(f.CustomerId), true)).LastNoPurchase);
        Assert.Null(Assert.Single((await f.Send(new GetCustomersQuery(ZoneId: f.ZoneId), true)).Items).LastNoPurchase);
        await Assert.ThrowsAsync<ConflictException>(() => f.Send(Visit(f)));
        Assert.Equal(2, await f.Sales());
        Assert.Equal(1, (await f.Send(new GetSessionDeliveriesQuery(f.SessionId))).Count(d => d.Type == DeliveryType.NoPurchase));
    }

    [Fact]
    public async Task Concurrent_visits_and_retries_produce_one_row()
    {
        var f = await new Scenario(fixture).Seed(); var command = Visit(f);
        var results = await Task.WhenAll(f.Send(command), f.Send(command));
        Assert.Equal(results[0], results[1]);
        await Task.WhenAll(f.Send(Visit(f)), f.Send(Visit(f) with { NoPurchaseReason = NoPurchaseReason.NoMoney }));
        Assert.Equal(1, await f.Sales()); Assert.Equal(20, await f.Stock());
    }

    [Fact]
    public async Task Unvisited_uses_current_route_and_excludes_every_visit_type_open_and_closed()
    {
        var f = await new Scenario(fixture).Seed();
        var early = f.Customer(1); var late = f.Customer(20); var containers = f.Customer(3);
        var promo = f.Customer(4); var sale = f.Customer(5); var wrongDay = f.Customer(2, [7]);
        var inactive = f.Customer(6); inactive.IsActive = false;
        await f.Write(db => { db.Customers.AddRange(early, late, containers, promo, sale, wrongDay, inactive); return Task.CompletedTask; });
        await f.Send(Visit(f));
        await f.Send(f.Sale(1) with { CustomerId = sale.Id });
        // El tipo antiguo se prepara como historial, nunca mediante el registro de visitas.
        await f.Write(db =>
        {
            db.Deliveries.Add(new GMSoft.Domain.Entities.Delivery {
                DeliverySessionId = f.SessionId, CustomerId = promo.Id,
                Type = DeliveryType.Promotion, DeliveredAt = DateTime.UtcNow });
            // Tiene un envase en su poder: la visita sin venta lo retira.
            db.CustomerContainerBalances.Add(new GMSoft.Domain.Entities.CustomerContainerBalance {
                CustomerId = containers.Id, ProductId = f.ProductId, Quantity = 1 });
            return Task.CompletedTask;
        });
        await f.Send(new RegisterDeliveryCommand(containers.Id, null, DeliveryType.ContainerOnly,
            [], [], [new(f.ProductId, 1)], null, null, Guid.NewGuid()));
        var result = await f.Send(new GetUnvisitedCustomersQuery(f.SessionId));
        Assert.Equal(new[] { early.Id, late.Id }, result.Items.Select(c => c.Id)); Assert.Equal(2, result.Total);
        await f.Send(f.Close(19), true);
        result = await f.Send(new GetUnvisitedCustomersQuery(f.SessionId), true);
        Assert.Equal(new[] { early.Id, late.Id }, result.Items.Select(c => c.Id));
        // Estos dos clientes se dieron de alta después de abrir: esa salida no suma actividad.
        Assert.All(result.Items, c => Assert.Equal(0, c.WeeksWithoutPurchase));
        var visited = await f.Send(new GetCustomerByIdQuery(f.CustomerId), true);
        Assert.Equal(1, visited.WeeksWithoutPurchase); Assert.Null(visited.LastPurchaseAt);
        Assert.NotNull(visited.LastNoPurchase);
        var other = await new Scenario(fixture).Seed();
        await Assert.ThrowsAsync<ForbiddenException>(() => other.Send(new GetUnvisitedCustomersQuery(f.SessionId)));
        await Assert.ThrowsAsync<ConflictException>(() => f.Send(Visit(f)));
    }

    [Theory]
    [InlineData("reason")][InlineData("other")][InlineData("items")][InlineData("containers")][InlineData("payment")][InlineData("key")]
    public async Task Invalid_visits_are_rejected_without_rows(string field)
    {
        var f = await new Scenario(fixture).Seed(); var command = Visit(f);
        command = field switch {
            "reason" => command with { NoPurchaseReason = null },
            "other" => command with { NoPurchaseReason = NoPurchaseReason.Other },
            "items" => command with { Items = [new(f.ProductId, 1)] },
            "containers" => command with { ContainersIn = [new(f.ProductId, 1)] },
            "payment" => command with { Payment = new(1, PaymentMethod.Cash) },
            _ => command with { ClientRequestId = null }
        };
        await Assert.ThrowsAsync<GMSoft.Application.Common.Exceptions.ValidationException>(() => f.Send(command));
        Assert.Equal(0, await f.Sales()); Assert.Equal(20, await f.Stock());
    }
}
