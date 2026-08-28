using Mapster;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.Products.Common;
using MediatR;

namespace GMSoft.Application.Features.Products.GetList;

public class GetProductsQueryHandler
    : IRequestHandler<GetProductsQuery, PagedResult<ProductDto>>
{
    private readonly IProductRepository _products;

    public GetProductsQueryHandler(IProductRepository products)
    {
        _products = products;
    }

    public async Task<PagedResult<ProductDto>> Handle(
        GetProductsQuery request,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _products.GetPagedAsync(
            request.Page,
            request.PageSize,
            request.Search,
            request.OnlyPublished,
            cancellationToken);

        return new PagedResult<ProductDto>(
            items.Adapt<List<ProductDto>>(),
            totalCount,
            request.Page,
            request.PageSize);
    }
}
