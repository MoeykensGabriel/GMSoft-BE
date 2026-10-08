using FluentValidation;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Promotions.GetSettings;
using MediatR;

namespace GMSoft.Application.Features.Promotions.UpdateSettings;

public record UpdatePromotionSettingsCommand(int PickupDays) : IRequest<PromotionSettingsDto>;
public class UpdatePromotionSettingsCommandValidator : AbstractValidator<UpdatePromotionSettingsCommand>
{
    public UpdatePromotionSettingsCommandValidator() => RuleFor(x => x.PickupDays).GreaterThan(0);
}
public class UpdatePromotionSettingsCommandHandler(IPromotionSettingsRepository settings)
    : IRequestHandler<UpdatePromotionSettingsCommand, PromotionSettingsDto>
{
    public async Task<PromotionSettingsDto> Handle(UpdatePromotionSettingsCommand r, CancellationToken ct)
    {
        await settings.SetPickupDaysAsync(r.PickupDays, ct);
        return new(r.PickupDays);
    }
}

