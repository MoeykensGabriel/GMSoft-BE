using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace GMSoft.Data.Context;

/// <summary>
/// Búsqueda que no distingue acentos: "pena" encuentra "Peña" y "jose" a "José".
/// La columna se pasa por <c>translate()</c> de Postgres (no hace falta la extensión unaccent)
/// y el texto buscado se limpia acá. Mayúsculas y minúsculas las resuelve ILIKE.
/// </summary>
public static class SearchText
{
    private const string Accented = "áàäâãéèëêíìïîóòöôõúùüûñçÁÀÄÂÃÉÈËÊÍÌÏÎÓÒÖÔÕÚÙÜÛÑÇ";
    private const string Plain    = "aaaaaeeeeiiiiooooouuuuncAAAAAEEEEIIIIOOOOOUUUUNC";

    /// <summary>Se traduce a SQL; no se puede llamar en memoria.</summary>
    public static string Fold(string value) => throw new InvalidOperationException("Solo se usa dentro de una consulta.");

    /// <summary>Patrón "contiene" para ILIKE: sin acentos y con los comodines escapados.</summary>
    public static string Contains(string search)
    {
        var folded = new StringBuilder();
        foreach (var ch in search.Trim().Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) folded.Append(ch);
        var term = folded.ToString().Normalize(NormalizationForm.FormC)
            .Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        return $"%{term}%";
    }

    internal static void Register(ModelBuilder modelBuilder)
        => modelBuilder.HasDbFunction(typeof(SearchText).GetMethod(nameof(Fold))!)
            .HasTranslation(args => new SqlFunctionExpression("translate",
                [args[0], new SqlConstantExpression(Accented, args[0].TypeMapping), new SqlConstantExpression(Plain, args[0].TypeMapping)],
                nullable: true, argumentsPropagateNullability: [true, false, false], typeof(string), args[0].TypeMapping));
}
