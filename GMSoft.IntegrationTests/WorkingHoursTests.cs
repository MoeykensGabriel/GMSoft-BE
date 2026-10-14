using GMSoft.Application.Common;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Features.Deliveries.Register;
using GMSoft.Application.Features.Promotions.Register;
using GMSoft.Application.Features.Promotions.Close;
using GMSoft.Application.Features.Sessions.Open;
using GMSoft.Application.Features.Sessions.GetById;
using GMSoft.Application.Features.Sessions.GetDeliveries;
using GMSoft.Application.Features.Sessions.DailySummary;
using GMSoft.Application.Features.Sessions.Settlement;
using GMSoft.Application.Features.VehicleLoads.Register;
using GMSoft.Application.Features.WorkingHours;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class WorkingHoursTests(PostgresFixture fixture)
{
    private static WorkingDay[] Week(bool working = true) => Enumerable.Range(1, 7)
        .Select(d => new WorkingDay(d, working, working ? new TimeOnly(8, 0) : null,
            working ? new TimeOnly(18, 0) : null, working ? new TimeOnly(16, 0) : null)).ToArray();

    private static async Task<T> At<T>(Scenario f, IRequest<T> command, string utc, bool admin = false)
    {
        using var scope = f.Scope(admin);
        scope.ServiceProvider.GetRequiredService<TestClock>().Now = DateTimeOffset.Parse(utc);
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command);
    }

    private static Task Clear(Scenario f) => f.Write(async db =>
        await db.Set<WorkingHoursSettings>().ExecuteDeleteAsync());

    [Fact, Trait("Grupo", "Horario laboral")]
    public async Task Configuration_is_absent_by_default_admin_only_validated_and_atomic()
    {
        var f = await new Scenario(fixture).Seed(false);
        try
        {
            var empty = await f.Send(new GetWorkingHoursQuery());
            Assert.False(empty.IsConfigured); Assert.Empty(empty.Days);
            await Assert.ThrowsAsync<ForbiddenException>(() => f.Send(new UpdateWorkingHoursCommand(Week())));
            await Assert.ThrowsAsync<ValidationException>(() => f.Send(new UpdateWorkingHoursCommand(Week()[..6]), true));
            Assert.False((await f.Send(new GetWorkingHoursQuery())).IsConfigured);
            await Task.WhenAll(f.Send(new UpdateWorkingHoursCommand(Week()), true),
                f.Send(new UpdateWorkingHoursCommand(Week(false)), true));
            Assert.Equal(1, await f.Read(db => db.Set<WorkingHoursSettings>().CountAsync()));
            var days = (await f.Send(new GetWorkingHoursQuery())).Days;
            Assert.Equal(7, days.Count);
            Assert.True(days.All(d => d.IsWorkingDay) || days.All(d => !d.IsWorkingDay));
            await Assert.ThrowsAsync<ValidationException>(() => f.Send(new UpdateWorkingHoursCommand([]), true));
            Assert.Equal(days, (await f.Send(new GetWorkingHoursQuery())).Days);
        }
        finally { await Clear(f); }
    }

    [Fact, Trait("Grupo", "Horario laboral")]
    public async Task Initial_load_blocks_marks_override_and_retry_preserves_original_after_settings_change()
    {
        var f = await new Scenario(fixture).Seed(false);
        var command = new RegisterVehicleLoadCommand(f.VehicleId, [new(f.ProductId, 5)], [1], Guid.NewGuid());
        try
        {
            await f.Send(new UpdateWorkingHoursCommand(Week()), true);
            await Assert.ThrowsAsync<OutsideWorkingHoursException>(() => At(f, command, "2026-10-12T22:00:00Z", true));
            Assert.Equal(0, await f.Read(db => db.VehicleLoads.CountAsync(l => l.VehicleId == f.VehicleId)));
            var first = await At(f, command with { ConfirmOutsideWorkingHours = true }, "2026-10-12T22:00:00Z", true);
            await f.Send(new UpdateWorkingHoursCommand(Week(false)), true);
            Assert.Equal(first, await At(f, command, "2026-10-13T12:00:00Z", true));
            var load = Assert.Single(await f.Read(db => db.VehicleLoads.Where(l => l.VehicleId == f.VehicleId).ToListAsync()));
            Assert.True(load.IsOutsideWorkingHours); Assert.Equal(f.AdminId, load.RegisteredByUserId);
            Assert.Equal(first.LoadedAt, load.LoadedAt);
            await Assert.ThrowsAsync<ForbiddenException>(() => At(f, command with { ConfirmOutsideWorkingHours = true }, "2026-10-13T12:00:00Z"));
        }
        finally { await Clear(f); }
    }

    [Theory, Trait("Grupo", "Horario laboral")]
    [InlineData("2026-10-12T11:00:00Z", false)]
    [InlineData("2026-10-12T19:00:00Z", false)]
    [InlineData("2026-10-12T19:00:01Z", true)]
    [InlineData("2026-10-12T21:00:00Z", true)]
    public async Task Opening_observes_cutoff_driver_cannot_override_admin_can(string utc, bool outside)
    {
        var f = await new Scenario(fixture).Seed(false); await f.Load();
        try
        {
            await f.Send(new UpdateWorkingHoursCommand(Week()), true);
            var command = new OpenSessionCommand(f.ZoneId, 100, true);
            if (outside)
            {
                await Assert.ThrowsAsync<OutsideWorkingHoursException>(() => At(f, command, utc));
                Assert.False(await f.Read(db => db.DeliverySessions.AnyAsync(s => s.VehicleId == f.VehicleId)));
                await Assert.ThrowsAsync<ForbiddenException>(() => At(f, command with { DriverId = f.DriverId }, utc));
                f.SessionId = await At(f, command with { DriverId = f.DriverId }, utc, true);
            }
            else f.SessionId = await At(f, command, utc);
            var session = await f.Send(new GetSessionByIdQuery(f.SessionId), true);
            Assert.Equal(outside, session.OpenedOutsideWorkingHours);
            Assert.Equal(outside ? f.AdminId : f.UserId, session.OpenedByUserId);
        }
        finally { await Clear(f); }
    }

    [Fact, Trait("Grupo", "Horario laboral")]
    public async Task Nonworking_day_blocks_opening_without_consuming_load()
    {
        var f = await new Scenario(fixture).Seed(false); await f.Load();
        try
        {
            await f.Send(new UpdateWorkingHoursCommand(Week(false)), true);
            await Assert.ThrowsAsync<OutsideWorkingHoursException>(() => At(f, new OpenSessionCommand(f.ZoneId, 100), "2026-10-12T15:00:00Z"));
            Assert.All(await f.Read(db => db.VehicleLoads.Where(l => l.VehicleId == f.VehicleId).ToListAsync()), l => Assert.Null(l.ConsumedBySessionId));
        }
        finally { await Clear(f); }
    }

    [Fact, Trait("Grupo", "Horario laboral")]
    public async Task All_operations_persist_independent_marks_and_queries_keep_them_after_configuration_changes()
    {
        var f = await new Scenario(fixture).Seed(false);
        const string late = "2026-10-12T22:00:00Z";
        try
        {
            await f.Send(new UpdateWorkingHoursCommand(Week()), true);
            await At(f, new RegisterVehicleLoadCommand(f.VehicleId, [new(f.ProductId, 20), new(f.NoneId, 20), new(f.UnitId, 20)], [1], Guid.NewGuid(), true), late, true);
            f.SessionId = await At(f, new OpenSessionCommand(f.ZoneId, 100, true, f.DriverId), late, true);
            var restock = f.Restock();
            await Assert.ThrowsAsync<OutsideWorkingHoursException>(() => At(f, restock, late, true));
            var added = await At(f, restock with { ConfirmOutsideWorkingHours = true }, late, true);
            Assert.True(added.IsOutsideWorkingHours);
            var visit = new RegisterDeliveryCommand(f.CustomerId, null, DeliveryType.NoPurchase, [], [], [], null, null, Guid.NewGuid(), NoPurchaseReason.NotHome);
            var visited = await At(f, visit, late);
            var sale = f.Sale(); var sold = await At(f, sale, late);
            var promo = new RegisterPromotionCommand(new(null, "Prueba", "123", "Calle", null, [1]), [new(f.ProductId, 1)], Guid.NewGuid());
            var promotion = await At(f, promo, late);
            Assert.True(promotion.IsOutsideWorkingHours);
            var closePromo = new ClosePromotionCommand(promotion.PromotionId, Guid.NewGuid(), false, null, [new(f.ProductId, 1)]);
            var closedPromo = await At(f, closePromo, late);
            Assert.True(closedPromo.ClosedOutsideWorkingHours);
            await At(f, f.Close(), late, true);
            var settlement = await At(f, new RegisterSettlementCommand(f.SessionId, 0, "Tarde"), late, true);
            Assert.True(settlement.IsOutsideWorkingHours);
            await Clear(f);
            Assert.Equal(added.Id, (await At(f, restock, "2026-10-13T15:00:00Z", true)).Id);
            Assert.Equal(visited.DeliveryId, (await At(f, visit, "2026-10-13T15:00:00Z")).DeliveryId);
            Assert.Equal(sold, await At(f, sale, "2026-10-13T15:00:00Z"));
            Assert.Equal(closedPromo, await At(f, closePromo, "2026-10-13T15:00:00Z"));
            Assert.Equal(closedPromo, await At(f, promo, "2026-10-13T15:00:00Z"));
            var session = await f.Send(new GetSessionByIdQuery(f.SessionId), true);
            Assert.True(session.OpenedOutsideWorkingHours); Assert.True(session.ClosedOutsideWorkingHours);
            Assert.True(session.SettlementOutsideWorkingHours); Assert.True(Assert.Single(session.Restocks).IsOutsideWorkingHours);
            Assert.Equal(f.AdminId, session.ClosedByUserId);
            Assert.All(await f.Send(new GetSessionDeliveriesQuery(f.SessionId)), v => Assert.True(v.IsOutsideWorkingHours));
            var summary = Assert.Single((await f.Send(new GetDailySummaryQuery(f.VehicleId, new(2026, 10, 12)), true)).Sessions);
            Assert.True(summary.OpenedOutsideWorkingHours); Assert.True(summary.ClosedOutsideWorkingHours);
            Assert.True(summary.SettlementOutsideWorkingHours);
            Assert.Equal(session.OutsideWorkingHoursEvents, summary.OutsideWorkingHoursEvents);
            Assert.Equal(11, summary.OutsideWorkingHoursEvents.Count);
            Assert.Equal(new[] { "initial-load", "promotion", "promotion-close", "restock", "session-close", "session-open", "settlement", "visit" },
                summary.OutsideWorkingHoursEvents.Select(e => e.Operation).Distinct().Order());
        }
        finally { await Clear(f); }
    }

    [Fact, Trait("Grupo", "Horario laboral")]
    public async Task Late_reception_does_not_change_in_hours_opening_mark()
    {
        var f = await new Scenario(fixture).Seed(false); await f.Load();
        try
        {
            await f.Send(new UpdateWorkingHoursCommand(Week()), true);
            f.SessionId = await At(f, new OpenSessionCommand(f.ZoneId, 100), "2026-10-12T15:00:00Z");
            await At(f, f.Close(), "2026-10-12T22:00:00Z", true);
            var session = await f.Send(new GetSessionByIdQuery(f.SessionId), true);
            Assert.False(session.OpenedOutsideWorkingHours); Assert.True(session.ClosedOutsideWorkingHours);
        }
        finally { await Clear(f); }
    }
}
