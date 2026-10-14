using FluentValidation;
using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Customers.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace GMSoft.Application.Features.Customers.ActivitySettings;

/// <summary>Los umbrales de la configuración de la API, para cuando ADMIN no eligió otros.</summary>
public record CustomerActivityDefaults(int RedAfterMissedWeeks, int BlackAfterMissedWeeks);

/// <param name="IsDefault">True mientras ADMIN no guardó umbrales propios.</param>
public record CustomerActivitySettingsDto(int RedAfterMissedWeeks, int BlackAfterMissedWeeks, bool IsDefault);

public static class CustomerActivityRegistration
{
    /// <summary>
    /// La política se arma por pedido con los umbrales que eligió ADMIN; sin ellos, con
    /// los de la configuración. Así un cambio se ve en el próximo pedido, sin reiniciar.
    /// </summary>
    public static IServiceCollection AddCustomerActivityPolicy(this IServiceCollection services, int red, int black)
    {
        _ = new CustomerActivityPolicy(red, black); // Valida al iniciar.
        var defaults = new CustomerActivityDefaults(red, black);
        services.AddSingleton(defaults);
        services.AddScoped(sp => sp.GetRequiredService<ICustomerActivitySettingsRepository>().Get() is { } s
            ? new CustomerActivityPolicy(s.RedAfterMissedWeeks, s.BlackAfterMissedWeeks)
            : new CustomerActivityPolicy(defaults.RedAfterMissedWeeks, defaults.BlackAfterMissedWeeks));
        return services;
    }
}

public record GetCustomerActivitySettingsQuery : IRequest<CustomerActivitySettingsDto>;

public class GetCustomerActivitySettingsQueryHandler(
    ICustomerActivitySettingsRepository repository, CustomerActivityDefaults defaults)
    : IRequestHandler<GetCustomerActivitySettingsQuery, CustomerActivitySettingsDto>
{
    public async Task<CustomerActivitySettingsDto> Handle(GetCustomerActivitySettingsQuery request, CancellationToken ct)
        => await repository.GetAsync(ct) is { } s
            ? new(s.RedAfterMissedWeeks, s.BlackAfterMissedWeeks, false)
            : new(defaults.RedAfterMissedWeeks, defaults.BlackAfterMissedWeeks, true);
}

public record UpdateCustomerActivitySettingsCommand(int RedAfterMissedWeeks, int BlackAfterMissedWeeks)
    : IRequest<CustomerActivitySettingsDto>;

public class UpdateCustomerActivitySettingsCommandValidator : AbstractValidator<UpdateCustomerActivitySettingsCommand>
{
    public UpdateCustomerActivitySettingsCommandValidator()
    {
        RuleFor(x => x.RedAfterMissedWeeks).InclusiveBetween(1, 51)
            .WithMessage("El rojo tiene que ser entre 1 y 51 semanas.");
        RuleFor(x => x.BlackAfterMissedWeeks).InclusiveBetween(2, 52)
            .WithMessage("El negro tiene que ser entre 2 y 52 semanas.")
            .GreaterThan(x => x.RedAfterMissedWeeks)
            .WithMessage("El negro tiene que llegar después que el rojo.");
    }
}

public class UpdateCustomerActivitySettingsCommandHandler(
    ICustomerActivitySettingsRepository repository, ICurrentUserService user)
    : IRequestHandler<UpdateCustomerActivitySettingsCommand, CustomerActivitySettingsDto>
{
    public async Task<CustomerActivitySettingsDto> Handle(UpdateCustomerActivitySettingsCommand request, CancellationToken ct)
    {
        if (!user.IsInRole(AppRoles.Admin))
            throw new ForbiddenException("Solo ADMIN puede cambiar los umbrales de actividad.");
        await repository.SetAsync(request.RedAfterMissedWeeks, request.BlackAfterMissedWeeks, ct);
        return new(request.RedAfterMissedWeeks, request.BlackAfterMissedWeeks, false);
    }
}
