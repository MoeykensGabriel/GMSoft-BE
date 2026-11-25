using GMSoft.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class AccentInsensitiveSearchTests(PostgresFixture fixture)
{
    [Theory]
    [InlineData("pena")]       // sin tilde ni eñe
    [InlineData("PEÑA")]       // mayúsculas
    [InlineData("jose")]
    [InlineData("belgrano")]   // dirección
    [InlineData("tucumán")]    // con tilde busca lo que está sin tilde
    public async Task Busca_clientes_sin_importar_acentos(string search)
    {
        var f = await new Scenario(fixture).Seed();
        await f.Write(async db =>
        {
            var customer = await db.Customers.SingleAsync(c => c.Id == f.CustomerId);
            customer.ContactName = "José Peña";
            customer.Address = "Belgrano 376, Tucuman";
            await db.SaveChangesAsync();
        });

        var found = await Search(f, search);

        Assert.Contains(f.CustomerId, found);
    }

    [Fact]
    public async Task Los_comodines_se_buscan_literales()
    {
        var f = await new Scenario(fixture).Seed();
        Assert.DoesNotContain(f.CustomerId, await Search(f, "%"));
    }

    private static Task<List<Guid>> Search(Scenario f, string search) => f.Read(async db =>
    {
        var (items, _) = await new CustomerRepository(db).GetPagedAsync(1, 50, search, null, null, null);
        return items.Select(c => c.Id).ToList();
    });
}
