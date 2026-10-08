using GMSoft.Application.Common.Interfaces.Repositories;
using MediatR;

namespace GMSoft.Application.Features.Promotions.GetSettings;

public record PromotionSettingsDto(int PickupDays);
public record GetPromotionSettingsQuery : IRequest<PromotionSettingsDto>;
public class GetPromotionSettingsQueryHandler(IPromotionSettingsRepository settings)
    : IRequestHandler<GetPromotionSettingsQuery, PromotionSettingsDto>
{
    public async Task<PromotionSettingsDto> Handle(GetPromotionSettingsQuery r, CancellationToken ct) =>
        new(await settings.GetPickupDaysAsync(ct));
}

