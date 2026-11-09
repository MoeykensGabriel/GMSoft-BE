using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Features.Sessions.ActiveDepartures;
using GMSoft.Application.Features.Sessions.GetById;
using GMSoft.Application.Features.Sessions.Transfer;
using GMSoft.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class TransferStockTests(PostgresFixture fixture)
{
    private static TransferStockCommand Pass(Scenario from, Scenario to, int quantity) =>
        new(from.SessionId, to.SessionId, [new(from.ProductId, quantity)]);

    [Fact]
    public async Task Takes_from_one_truck_and_adds_to_the_other_at_once()
    {
        var from = await new Scenario(fixture).Seed(stock: 20);
        var to = await new Scenario(fixture).Seed(stock: 20);

        var result = await from.Send(Pass(from, to, 3), admin: true);

        Assert.Equal("Camion", result.FromVehicle);
        Assert.Equal(3, Assert.Single(result.Lines).Quantity);
        Assert.Equal(17, await from.Stock());
        Assert.Equal(3, await to.Stock(product: from.ProductId));
        var notes = await from.Read(db => db.SessionStockMovements
            .Where(m => m.Type == SessionStockMovementType.TransferOut || m.Type == SessionStockMovementType.TransferIn)
            .Where(m => m.DeliverySessionId == from.SessionId || m.DeliverySessionId == to.SessionId)
            .Select(m => new { m.Type, m.Quantity, m.Notes, m.RegisteredByUserId }).ToListAsync());
        Assert.Equal(2, notes.Count);
        Assert.All(notes, n => Assert.Equal(from.AdminId, n.RegisteredByUserId));
        Assert.Contains(notes, n => n.Type == SessionStockMovementType.TransferOut && n.Quantity == -3);
        Assert.Contains(notes, n => n.Type == SessionStockMovementType.TransferIn && n.Quantity == 3);
    }

    [Fact]
    public async Task The_pass_shows_in_both_departures_and_on_the_home_table()
    {
        var from = await new Scenario(fixture).Seed(stock: 20);
        var to = await new Scenario(fixture).Seed(stock: 20);
        await from.Send(Pass(from, to, 3), admin: true);

        var received = await to.Send(new GetSessionByIdQuery(to.SessionId), admin: true);
        var pase = Assert.Single(received.Transfers);
        Assert.True(pase.Incoming);
        Assert.StartsWith("Pase de", pase.Notes);
        Assert.Equal(3, Assert.Single(pase.Items).Quantity);
        var given = await from.Send(new GetSessionByIdQuery(from.SessionId), admin: true);
        Assert.False(Assert.Single(given.Transfers).Incoming);

        var home = await from.Send(new GetActiveDeparturesQuery(), admin: true);
        var line = home.Single(d => d.SessionId == from.SessionId).Stock.Single(l => l.ProductId == from.ProductId);
        Assert.Equal(-3, line.Transferred);
        Assert.Equal(line.Initial + line.Restocked - line.Delivered + line.Transferred, line.FullOnBoard);
    }

    [Fact]
    public async Task Cannot_pass_more_than_the_truck_has_on_board()
    {
        var from = await new Scenario(fixture).Seed(stock: 2);
        var to = await new Scenario(fixture).Seed();

        await Assert.ThrowsAsync<ConflictException>(() => from.Send(Pass(from, to, 3), admin: true));

        Assert.Equal(2, await from.Stock());
        Assert.Equal(0, await to.Stock(product: from.ProductId));
    }

    [Fact]
    public async Task Both_trucks_must_still_be_on_the_street()
    {
        var from = await new Scenario(fixture).Seed();
        var to = await new Scenario(fixture).Seed();
        await to.Write(async db => (await db.DeliverySessions.FindAsync(to.SessionId))!.Status = SessionStatus.Closed);

        await Assert.ThrowsAsync<ConflictException>(() => from.Send(Pass(from, to, 1), admin: true));
        Assert.Equal(20, await from.Stock());
    }

    [Fact]
    public async Task Only_the_office_and_never_to_the_same_truck()
    {
        var from = await new Scenario(fixture).Seed();
        var to = await new Scenario(fixture).Seed();

        await Assert.ThrowsAsync<ForbiddenException>(() => from.Send(Pass(from, to, 1)));
        await Assert.ThrowsAsync<ValidationException>(() => from.Send(Pass(from, from, 1), admin: true));
    }
}
