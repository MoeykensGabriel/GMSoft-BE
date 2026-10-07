using System.Reflection;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Sessions.Common;
using GMSoft.Application.Features.Sessions.DetailedSettlement;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;

namespace GMSoft.Application.Tests.Features.Sessions;

public class DetailedSettlementTests
{
    [Fact]
    public async Task Groups_visits_by_customer_with_returns_payments_and_balance()
    {
        var session = new DeliverySession { Id = Guid.NewGuid(), ClosedAt = new DateTime(2026, 10, 6, 21, 0, 0, DateTimeKind.Utc) };
        var julieta = Guid.NewGuid();
        var micaela = Guid.NewGuid();
        var bidon = Guid.NewGuid();
        var sifon = Guid.NewGuid();
        var hora = new DateTime(2026, 10, 6, 13, 0, 0, DateTimeKind.Utc);

        IReadOnlyList<SessionDeliveryDto> deliveries =
        [
            new(Guid.NewGuid(), julieta, "Julieta Nuñez", "Psj Viete 2282", DeliveryType.Sale, hora, 5000m, null,
                [new(bidon, "BIDON 12LTS", 1, 4000m), new(sifon, "SIFON 1.5LTS", 2, 500m)],
                [new(bidon, "BIDON 12LTS", 1), new(bidon, "BIDON 12LTS", -1), new(sifon, "SIFON 1.5LTS", -2)])
            { CustomerPhone = "3815417440" },
            new(Guid.NewGuid(), micaela, "Micaela Acuña", "Psj Viete 2241", DeliveryType.Sale, hora.AddMinutes(5), 6500m, null,
                [new(bidon, "BIDON 20LTS", 1, 6500m)], []),
            // Segunda pasada por la misma clienta: tiene que caer en su mismo bloque.
            new(Guid.NewGuid(), julieta, "Julieta Nuñez", "Psj Viete 2282", DeliveryType.ContainerOnly, hora.AddMinutes(9), 0m, null,
                [], [new(bidon, "BIDON 12LTS", -1)]),
        ];
        IReadOnlyList<SessionCustomerPaymentDto> payments =
        [
            new(julieta, PaymentMethod.Cash, 3000m),
            new(julieta, PaymentMethod.Transfer, 1000m),
        ];
        IReadOnlyDictionary<Guid, decimal> balances = new Dictionary<Guid, decimal> { [julieta] = 1000m, [micaela] = 6500m };
        DateTime? cutoff = null;

        var handler = new GetSessionDetailedSettlementQueryHandler(Stub<ISessionRepository>((name, args) => name switch
        {
            "GetByIdAsync" => Task.FromResult<DeliverySession?>(session),
            "GetDeliveriesAsync" => Task.FromResult(deliveries),
            "GetPaymentsByCustomerAsync" => Task.FromResult(payments),
            "GetCustomerBalancesAsync" => Balances((DateTime)args![1]!),
            _ => throw new InvalidOperationException(name)
        }));
        Task<IReadOnlyDictionary<Guid, decimal>> Balances(DateTime until) { cutoff = until; return Task.FromResult(balances); }

        var result = await handler.Handle(new(session.Id), default);

        Assert.Equal(new[] { julieta, micaela }, result.Select(c => c.CustomerId));
        Assert.Equal(session.ClosedAt, cutoff);

        var primera = result[0];
        Assert.Equal("3815417440", primera.CustomerPhone);
        Assert.Equal(new[] { 4000m, 1000m }, primera.Lines.Select(l => l.Amount));
        // Solo lo devuelto, en positivo y sumado entre las dos visitas.
        Assert.Equal(new[] { ("BIDON 12LTS", 2), ("SIFON 1.5LTS", 2) },
            primera.ReturnedContainers.Select(c => (c.ProductDetail, c.Quantity)));
        Assert.Equal((3000m, 1000m, 0m, 1000m), (primera.Cash, primera.Transfer, primera.Card, primera.Balance));

        var segunda = result[1];
        Assert.Empty(segunda.ReturnedContainers);
        Assert.Equal((0m, 0m, 0m, 6500m), (segunda.Cash, segunda.Transfer, segunda.Card, segunda.Balance));
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
