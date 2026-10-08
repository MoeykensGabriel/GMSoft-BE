using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Promotions.Common;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.Promotions.GetById;

public record GetPromotionQuery(Guid Id) : IRequest<PromotionDto>;
public class GetPromotionQueryHandler(IPromotionRepository promotions) : IRequestHandler<GetPromotionQuery, PromotionDto>
{
    public async Task<PromotionDto> Handle(GetPromotionQuery r, CancellationToken ct) =>
        PromotionDto.From(await promotions.GetDetailsAsync(r.Id, ct) ?? throw new NotFoundException(nameof(Promotion), r.Id));
}

