using GMSoft.Application.Features.Promotions.Register;
using GMSoft.Application.Features.Sessions.SalesSummary;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class SessionSalesSummaryTests(PostgresFixture fixture)
{
    [Fact, Trait("Grupo", "Salidas")]
    public async Task The_driver_sees_what_he_sold_by_payment_method_and_product_without_promotions()
    {
        var f = await new Scenario(fixture).Seed();
        await f.Send(f.Sale(sold: 3, paid: true));
        await f.Send(f.Sale(sold: 2));
        await f.Send(new RegisterPromotionCommand(
            new(null, "Prospecto", "123", "Calle", null, [1]), [new(f.ProductId, 4)], Guid.NewGuid()));

        var resumen = await f.Send(new GetSessionSalesSummaryQuery(f.SessionId));

        // Bidón a $100: vendió 5, cobró 3 en efectivo y 2 quedaron a deuda.
        Assert.Equal(500, resumen.Total);
        Assert.Equal(300, resumen.Cash);
        Assert.Equal(0, resumen.Transfer);
        Assert.Equal(2, resumen.Sales);
        Assert.Equal(2, resumen.Visits);
        // Los 4 de la promoción no son ventas.
        var bidon = Assert.Single(resumen.Products);
        Assert.Equal(f.ProductId, bidon.ProductId);
        Assert.Equal(5, bidon.Quantity);

        // La oficina ve lo mismo.
        Assert.Equal(500, (await f.Send(new GetSessionSalesSummaryQuery(f.SessionId), true)).Total);
    }
}
