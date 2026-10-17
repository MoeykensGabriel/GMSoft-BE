using GMSoft.Application.Features.Customers.Account;
using GMSoft.Application.Features.Customers.GetById;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class CustomerDaysTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Cuenta_y_ficha_cuentan_los_dias_sin_comprar_en_hora_de_Argentina()
    {
        var f = await new Scenario(fixture).Seed();
        // Ayer a las 23 h de Argentina: en UTC ya es hoy, el caso en que las dos pantallas diferían.
        var argentina = DateTime.UtcNow.AddHours(-3).Date;
        var compra = argentina.AddHours(-1).AddHours(3);
        await f.Write(db =>
        {
            db.Deliveries.Add(new Delivery { CustomerId = f.CustomerId, DeliverySessionId = f.SessionId,
                Type = DeliveryType.Sale, DeliveredAt = DateTime.SpecifyKind(compra, DateTimeKind.Utc), Total = 100,
                Items = [new() { ProductId = f.ProductId, Quantity = 1, UnitPrice = 100 }] });
            return Task.CompletedTask;
        });
        var cuenta = await f.Send(new GetCustomerAccountQuery(f.CustomerId), true);
        var ficha = await f.Send(new GetCustomerByIdQuery(f.CustomerId), true);
        Assert.Equal(1, cuenta.DaysWithoutPurchase);
        Assert.Equal(ficha.DaysWithoutPurchase, cuenta.DaysWithoutPurchase);
    }
}
