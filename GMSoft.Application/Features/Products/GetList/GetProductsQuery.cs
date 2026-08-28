using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.Products.Common;
using MediatR;

namespace GMSoft.Application.Features.Products.GetList;

/// <summary>
/// Catálogo paginado. OnlyPublished en true es lo que ve el chofer para cargar
/// el camión; el admin lo deja en nulo para ver todo, publicado o no.
/// </summary>
public record GetProductsQuery(
    int     Page          = 1,
    int     PageSize      = 20,
    string? Search        = null,
    bool?   OnlyPublished = null) : IRequest<PagedResult<ProductDto>>;
