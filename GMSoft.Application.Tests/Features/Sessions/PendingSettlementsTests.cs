using System.Reflection;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Sessions.PendingSettlements;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;

namespace GMSoft.Application.Tests.Features.Sessions;

public class PendingSettlementsTests
{
    [Fact]
    public async Task Oldest_reception_first_with_the_cash_each_driver_has_to_deliver()
    {
        var yesterday = Received(DateTime.UtcNow.AddDays(-1));
        var today = Received(DateTime.UtcNow);
        today.Vehicle!.Name = "Camión 2";
        var repository = Stub<ISessionRepository>((name, _) => name switch
        {
            "GetClosedUnsettledAsync" => Task.FromResult<IReadOnlyList<DeliverySession>>([today, yesterday]),
            // La de hoy no cobró nada en efectivo: no viene en el diccionario.
            "GetCashCollectedAsync" => Task.FromResult<IReadOnlyDictionary<Guid, decimal>>(
                new Dictionary<Guid, decimal> { [yesterday.Id] = 52500m }),
            _ => throw new InvalidOperationException(name),
        });

        var result = await new GetPendingSettlementsQueryHandler(repository).Handle(new(), default);

        Assert.Equal(new[] { yesterday.Id, today.Id }, result.Select(p => p.SessionId));
        Assert.Equal(new PendingSettlementDto(yesterday.Id, "Ana Pérez", "Camión 1", "TEST001", "Centro",
            yesterday.ClosedAt!.Value, 52500m), result[0]);
        Assert.Equal(0m, result[1].CashToDeliver);
    }

    [Fact]
    public async Task Without_pending_settlements_payments_are_not_queried()
    {
        var repository = Stub<ISessionRepository>((name, _) => name == "GetClosedUnsettledAsync"
            ? Task.FromResult<IReadOnlyList<DeliverySession>>([])
            : throw new InvalidOperationException(name));
        Assert.Empty(await new GetPendingSettlementsQueryHandler(repository).Handle(new(), default));
    }

    private static DeliverySession Received(DateTime closedAt) => new()
    {
        Id = Guid.NewGuid(), VehicleId = Guid.NewGuid(), Status = SessionStatus.Closed,
        Vehicle = new() { Name = "Camión 1", LicensePlate = "TEST001" },
        Driver = new() { FirstName = "Ana", LastName = "Pérez" },
        Zone = new() { Name = "Centro" }, OpenedAt = closedAt.AddHours(-8), ClosedAt = closedAt
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
