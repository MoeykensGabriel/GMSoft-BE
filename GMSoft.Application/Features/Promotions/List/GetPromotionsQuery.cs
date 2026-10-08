using FluentValidation;
using GMSoft.Application.Common;
using GMSoft.Application.Common.Models;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Promotions.Common;
using MediatR;

namespace GMSoft.Application.Features.Promotions.List;

public record GetPromotionsQuery(int Page = 1, int PageSize = 20, string? Status = null,
    Guid? VehicleId = null, DateOnly? From = null, DateOnly? To = null) : IRequest<PagedResult<PromotionDto>>;
public class GetPromotionsQueryValidator : AbstractValidator<GetPromotionsQuery>
{
    public GetPromotionsQueryValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, int.MaxValue / 100);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Status).Must(s => s is null or "Pending" or "Overdue" or "Converted" or "NotConverted");
        RuleFor(x => x).Must(x => !x.From.HasValue || !x.To.HasValue || x.From <= x.To)
            .WithMessage("El rango de fechas es invalido.");
        RuleFor(x => x.To).Must(d => d is null || d < DateOnly.MaxValue);
    }
}
public class GetPromotionsQueryHandler(IPromotionRepository promotions)
    : IRequestHandler<GetPromotionsQuery, PagedResult<PromotionDto>>
{
    public async Task<PagedResult<PromotionDto>> Handle(GetPromotionsQuery r, CancellationToken ct)
    {
        var (items, count) = await promotions.GetPagedAsync(r.Page, r.PageSize, r.Status, r.VehicleId,
            r.From is { } from ? BusinessTime.DayRangeUtc(from).FromUtc : null,
            r.To is { } to ? BusinessTime.DayRangeUtc(to).ToUtc : null, PromotionDto.Today, ct);
        return new(items.Select(PromotionDto.From).ToList(), count, r.Page, r.PageSize);
    }
}

