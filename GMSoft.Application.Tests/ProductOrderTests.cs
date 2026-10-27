using GMSoft.Application.Common;

namespace GMSoft.Application.Tests;

public class ProductOrderTests
{
    [Theory]
    [InlineData("DISPENSER", true)]
    [InlineData("Equipo Díspénsér frío", true)]
    [InlineData("Di\u0301spenser", true)]
    [InlineData("Bidón 20 lts", false)]
    [InlineData("", false)]
    public void Recognizes_name_ignoring_case_and_accents(string detail, bool expected)
        => Assert.Equal(expected, ProductOrder.IsDispenser(detail));

    [Fact]
    public void Orders_each_group_by_detail_without_changing_source()
    {
        string[] products = ["Dispenser Z", "Sifon 1.5 lts", "Dispenser A", "Bidón 20 lts", "Bidón 12 lts"];
        var original = products.ToArray();
        Assert.Equal(["Bidón 12 lts", "Bidón 20 lts", "Sifon 1.5 lts", "Dispenser A", "Dispenser Z"],
            products.OrderByProduct(p => p));
        Assert.Equal(original, products);
        Assert.Empty(Array.Empty<string>().OrderByProduct(p => p));
    }
}
