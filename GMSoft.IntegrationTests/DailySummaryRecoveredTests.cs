using GMSoft.Application.Common;
using GMSoft.Application.Features.Sessions.DailySummary;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class DailySummaryRecoveredTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Daily_summary_shows_the_empties_customers_had_unregistered()
    {
        var f = await new Scenario(fixture).Seed();
        // Sin envases registrados: compra 3 y devuelve 2 que tenía. Después compra 1 más sin devolver.
        await f.Send(f.Sale(3, 2));
        await f.Send(f.Sale(1));

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow.Add(BusinessTime.Offset));
        var resumen = await f.Send(new GetDailySummaryQuery(f.VehicleId, hoy), true);
        var bidon = Assert.Single(Assert.Single(resumen.Sessions).Products, p => p.ProductId == f.ProductId);
        Assert.Equal(2, bidon.Stock.EmptyCollected);
        Assert.Equal(2, bidon.Stock.EmptyRecovered);
        Assert.Equal(2, resumen.DayTotals.Totals.EmptyRecovered);
    }
}
