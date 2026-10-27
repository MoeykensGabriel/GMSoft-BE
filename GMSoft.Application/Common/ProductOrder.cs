using System.Globalization;
using System.Text;

namespace GMSoft.Application.Common;

public static class ProductOrder
{
    public static bool IsDispenser(string detail)
    {
        var normalized = detail.Normalize(NormalizationForm.FormD);
        var plain = new string(normalized.Where(c => CharUnicodeInfo.GetUnicodeCategory(c)
            != UnicodeCategory.NonSpacingMark).ToArray());
        return plain.Contains("dispenser", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Productos por detalle, con todos los dispenser al final.</summary>
    public static IOrderedEnumerable<T> OrderByProduct<T>(this IEnumerable<T> source, Func<T, string> getDetail)
        => source.OrderBy(item => IsDispenser(getDetail(item))).ThenBy(getDetail);
}
