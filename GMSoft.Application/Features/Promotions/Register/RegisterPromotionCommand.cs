using FluentValidation;
using GMSoft.Application.Common;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Customers.Common;
using GMSoft.Application.Features.Deliveries.Register;
using GMSoft.Application.Features.Promotions.Common;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using MediatR;

namespace GMSoft.Application.Features.Promotions.Register;

public record RegisterPromotionCommand(NewCustomerLine Prospect, IReadOnlyList<DeliveryItemLine> Items,
    Guid ClientRequestId) : IRequest<PromotionResult>;

public class RegisterPromotionCommandValidator : AbstractValidator<RegisterPromotionCommand>
{
    public RegisterPromotionCommandValidator()
    {
        RuleFor(x => x.ClientRequestId).NotEmpty();
        RuleFor(x => x.Prospect).NotNull().SetValidator(new NewCustomerLineValidator());
        RuleFor(x => x.Items).NotEmpty();
        When(x => x.Items is not null, () =>
        {
            RuleFor(x => x.Items).Must(x => x.Select(i => i.ProductId).Distinct().Count() == x.Count)
                .WithMessage("Hay productos repetidos.");
            RuleForEach(x => x.Items).ChildRules(l =>
            {
                l.RuleFor(i => i.ProductId).NotEmpty();
                l.RuleFor(i => i.Quantity).GreaterThan(0);
            });
        });
    }
}

public class RegisterPromotionCommandHandler(IPromotionRepository promotions, IPromotionSettingsRepository settings,
    ISessionRepository sessions, IProductRepository products, ICurrentUserService user, IUnitOfWork uow)
    : IRequestHandler<RegisterPromotionCommand, PromotionResult>
{
    public async Task<PromotionResult> Handle(RegisterPromotionCommand request, CancellationToken ct)
    {
        var driverId = user.DriverId ?? throw new ForbiddenException("Solo un chofer registra promociones.");
        PromotionResult result = null!;
        await uow.ExecuteInTransactionAsync(async () =>
        {
            await promotions.LockAsync($"promotion-register:{request.ClientRequestId}", ct);
            var previous = await promotions.FindRequestAsync(request.ClientRequestId, false, ct);
            if (previous is not null)
            {
                if (previous.DriverId != driverId) throw new ForbiddenException("El envio pertenece a otro chofer.");
                result = PromotionResult.From(previous);
                return;
            }
            var session = await sessions.GetOpenByDriverAsync(driverId, ct)
                ?? throw new ConflictException("No tenes una salida abierta.");
            await promotions.LockAsync($"promotion-vehicle:{session.VehicleId}", ct);
            var resolved = new Dictionary<Guid, Product>();
            foreach (var item in request.Items)
            {
                var product = await products.GetByIdAsync(item.ProductId, ct)
                    ?? throw new NotFoundException(nameof(Product), item.ProductId);
                if (product.Tracking == ContainerTracking.ByUnit)
                    throw new BadRequestException("Los productos ByUnit se asignan por numero de serie, no en promociones.");
                resolved.Add(item.ProductId, product);
            }
            await DeliveryStockRules.EnsureAvailableAsync(sessions, session.Id, request.Items, resolved, ct);
            var now = DateTime.UtcNow;
            var days = await settings.GetPickupDaysAsync(ct);
            var localDate = DateOnly.FromDateTime(now.Add(BusinessTime.Offset));
            if (days > DateOnly.MaxValue.DayNumber - localDate.DayNumber)
                throw new ConflictException("El plazo configurado excede el rango de fechas admitido.");
            var p = new Promotion
            {
                BusinessName = string.IsNullOrWhiteSpace(request.Prospect.BusinessName) ? null : request.Prospect.BusinessName.Trim(),
                ContactName = request.Prospect.ContactName.Trim(), Phone = request.Prospect.Phone.Trim(),
                Address = request.Prospect.Address.Trim(), Notes = request.Prospect.Notes?.Trim(),
                VisitDays = request.Prospect.VisitDays!.Order().ToArray(),
                VehicleId = session.VehicleId, DriverId = driverId, DeliverySessionId = session.Id,
                ZoneId = session.ZoneId, RegisteredAt = now, PickupDate = localDate.AddDays(days),
                ClientRequestId = request.ClientRequestId
            };
            foreach (var item in request.Items)
            {
                var loaned = resolved[item.ProductId].Tracking == ContainerTracking.ByBalance ? item.Quantity : 0;
                p.Lines.Add(new PromotionLine { ProductId = item.ProductId, Quantity = item.Quantity, ContainersLoaned = loaned });
                session.StockMovements.Add(new SessionStockMovement
                {
                    DeliverySessionId = session.Id, ProductId = item.ProductId, Quantity = -item.Quantity,
                    State = ContainerState.Full, Type = SessionStockMovementType.Delivered,
                    OccurredAt = now, RegisteredByUserId = user.UserId, Notes = $"Promocion, envio {request.ClientRequestId}"
                });
                if (loaned > 0) PromotionMovements.Add(p, item.ProductId, loaned,
                    ContainerMovementType.DeliveredToCustomer, now, user.UserId, "Prestamo al prospecto");
            }
            await promotions.AddAsync(p, ct);
            sessions.Update(session);
            await uow.SaveChangesAsync(ct);
            result = PromotionResult.From(p);
        }, ct);
        return result;
    }
}

