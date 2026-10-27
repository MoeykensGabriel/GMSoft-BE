using FluentValidation;
using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.VehicleLoads.Ranges;

public record SaveVehicleLoadRangesCommand(Guid VehicleId, VehicleLoadRangeInput[] Ranges) : IRequest;

public class SaveVehicleLoadRangesCommandValidator : AbstractValidator<SaveVehicleLoadRangesCommand>
{
    public SaveVehicleLoadRangesCommandValidator()
    {
        RuleFor(x => x.VehicleId).NotEmpty().WithMessage("Elegí un vehículo.");
        RuleFor(x => x.Ranges).NotNull().WithMessage("Enviá la lista de rangos.");
        When(x => x.Ranges is not null, () =>
        {
            RuleFor(x => x.Ranges)
                .Must(rows => rows.Where(r => r is not null).Select(r => r.ProductId).Distinct().Count()
                    == rows.Count(r => r is not null))
                .WithMessage("No repitas productos en los rangos de carga.");
            RuleForEach(x => x.Ranges).NotNull().WithMessage("Cada rango debe tener un producto y al menos un límite.")
                .SetValidator(new VehicleLoadRangeInputValidator());
        });
    }
}

public class VehicleLoadRangeInputValidator : AbstractValidator<VehicleLoadRangeInput>
{
    public VehicleLoadRangeInputValidator()
    {
        RuleFor(x => x).Must(x => x.Min.HasValue || x.Max.HasValue)
            .WithMessage("Ingresá al menos el mínimo o el máximo.");
        RuleFor(x => x.Min).InclusiveBetween(0, 100000)
            .WithMessage("El mínimo debe estar entre 0 y 100000.");
        RuleFor(x => x.Max).InclusiveBetween(0, 100000)
            .WithMessage("El máximo debe estar entre 0 y 100000.");
        RuleFor(x => x).Must(x => !x.Min.HasValue || !x.Max.HasValue || x.Max >= x.Min)
            .WithMessage("El máximo debe ser mayor o igual al mínimo.");
    }
}

public class SaveVehicleLoadRangesCommandHandler(
    IVehicleLoadRangeRepository ranges, IVehicleRepository vehicles, IProductRepository products,
    ICurrentUserService user, IUnitOfWork work) : IRequestHandler<SaveVehicleLoadRangesCommand>
{
    public async Task Handle(SaveVehicleLoadRangesCommand request, CancellationToken cancellationToken)
    {
        if (!user.IsInRole(AppRoles.Admin))
            throw new ForbiddenException("Solo el administrador puede guardar los rangos de carga.");
        await work.ExecuteInTransactionAsync(async () =>
        {
            _ = await vehicles.LockAsync(request.VehicleId, cancellationToken)
                ?? throw new NotFoundException(nameof(Vehicle), request.VehicleId);
            var ids = request.Ranges.Select(r => r.ProductId).Distinct().ToArray();
            var existing = await products.GetExistingIdsAsync(ids, cancellationToken);
            var missing = ids.Except(existing).ToArray();
            if (missing.Length > 0)
                throw new BadRequestException($"No existen estos productos: {string.Join(", ", missing)}.");
            await ranges.ReplaceAsync(request.VehicleId, request.Ranges, cancellationToken);
            await work.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
    }
}
