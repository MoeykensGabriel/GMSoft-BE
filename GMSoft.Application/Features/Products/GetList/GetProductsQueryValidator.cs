using FluentValidation;

namespace GMSoft.Application.Features.Products.GetList;

public class GetProductsQueryValidator : AbstractValidator<GetProductsQuery>
{
    public GetProductsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);

        // Tope de página: sin esto, un pageSize enorme se convierte en una forma
        // barata de tirar abajo la API.
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
