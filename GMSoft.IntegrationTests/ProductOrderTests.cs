using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class ProductOrderTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Catalog_orders_before_pagination_including_accented_names()
    {
        var scenario = await new Scenario(fixture).Seed(open: false);
        await scenario.Write(async db =>
        {
            (await db.Products.SingleAsync(p => p.Id == scenario.NoneId)).Detail = "Sifon 1.5 lts";
            (await db.Products.SingleAsync(p => p.Id == scenario.UnitId)).Detail = "DÍSPÉNSER";
        });
        using var scope = scenario.Scope();
        var repository = scope.ServiceProvider.GetRequiredService<IProductRepository>();
        var all = await repository.GetPagedAsync(1, int.MaxValue, true);
        Assert.Equal(all.Items.OrderByProduct(p => p.Detail).Select(p => p.Id), all.Items.Select(p => p.Id));
        var dispenserIndex = all.Items.ToList().FindIndex(p => p.Id == scenario.UnitId);
        Assert.True(dispenserIndex > all.Items.ToList().FindIndex(p => p.Id == scenario.NoneId));
        var page = await repository.GetPagedAsync(dispenserIndex + 1, 1, true);
        Assert.Equal(scenario.UnitId, Assert.Single(page.Items).Id);
        Assert.Equal(all.TotalCount, page.TotalCount);
    }

    [Fact]
    public async Task Real_stock_lists_put_accented_dispenser_after_sifon_without_changing_quantities()
    {
        var scenario = await new Scenario(fixture).Seed();
        await scenario.Write(async db =>
        {
            (await db.Products.SingleAsync(p => p.Id == scenario.NoneId)).Detail = "Sifon 1.5 lts";
            (await db.Products.SingleAsync(p => p.Id == scenario.UnitId)).Detail = "DÍSPÉNSER";
        });
        using var scope = scenario.Scope();
        var repository = scope.ServiceProvider.GetRequiredService<ISessionRepository>();
        var single = await repository.GetStockBalanceAsync(scenario.SessionId);
        var batch = (await repository.GetStockBalancesAsync([scenario.SessionId]))[scenario.SessionId];
        foreach (var stock in new[] { single, batch })
        {
            Assert.Equal([scenario.ProductId, scenario.NoneId, scenario.UnitId], stock.Select(p => p.ProductId));
            Assert.All(stock, p => Assert.Equal((20, 0), (p.FullOnBoard, p.EmptyOnBoard)));
        }
    }
}
