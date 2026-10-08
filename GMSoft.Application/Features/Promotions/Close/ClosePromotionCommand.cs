using FluentValidation;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Customers.Common;
using GMSoft.Application.Features.Deliveries.Register;
using GMSoft.Application.Features.Promotions.Common;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using MediatR;

namespace GMSoft.Application.Features.Promotions.Close;

public record ClosePromotionCommand(Guid PromotionId, Guid ClientRequestId, bool ConvertToCustomer,
    NewCustomerLine? Customer, IReadOnlyList<ContainerLine> ContainersReturned) : IRequest<PromotionResult>;

public class ClosePromotionCommandValidator : AbstractValidator<ClosePromotionCommand>
{
    public ClosePromotionCommandValidator()
    {
        RuleFor(x => x.PromotionId).NotEmpty();
        RuleFor(x => x.ClientRequestId).NotEmpty();
        RuleFor(x => x.ContainersReturned).NotNull();
        When(x => x.ConvertToCustomer, () =>
        {
            RuleFor(x => x.ContainersReturned).Empty();
            When(x => x.Customer is not null, () => RuleFor(x => x.Customer!).SetValidator(new NewCustomerLineValidator()));
        });
        When(x => !x.ConvertToCustomer, () => RuleFor(x => x.Customer).Null());
        When(x => x.ContainersReturned is not null, () =>
        {
            RuleFor(x => x.ContainersReturned).Must(x => x.Select(i => i.ProductId).Distinct().Count() == x.Count)
                .WithMessage("Hay productos repetidos.");
            RuleForEach(x => x.ContainersReturned).ChildRules(l =>
            {
                l.RuleFor(i => i.ProductId).NotEmpty();
                l.RuleFor(i => i.Quantity).GreaterThanOrEqualTo(0);
            });
        });
    }
}

public class ClosePromotionCommandHandler(IPromotionRepository promotions, ISessionRepository sessions,
    ICustomerRepository customers, IContainerBalanceRepository balances, ICurrentUserService user, IUnitOfWork uow)
    : IRequestHandler<ClosePromotionCommand, PromotionResult>
{
    public async Task<PromotionResult> Handle(ClosePromotionCommand request, CancellationToken ct)
    {
        var driverId = user.DriverId ?? throw new ForbiddenException("Solo un chofer cierra promociones.");
        PromotionResult result = null!;
        await uow.ExecuteInTransactionAsync(async () =>
        {
            await promotions.LockAsync($"promotion-close-request:{request.ClientRequestId}", ct);
            var previous = await promotions.FindRequestAsync(request.ClientRequestId, true, ct);
            if (previous is not null)
            {
                if (previous.ClosedByDriverId != driverId) throw new ForbiddenException("El cierre pertenece a otro chofer.");
                if (previous.Id != request.PromotionId ||
                    (previous.Status == PromotionStatus.Converted) != request.ConvertToCustomer)
                    throw new ConflictException("El identificador ya se uso para otro cierre.");
                result = PromotionResult.From(previous);
                return;
            }
            // Lock por promocion antes de leer su estado: dos claves diferentes no pueden cerrar dos veces.
            await promotions.LockAsync($"promotion:{request.PromotionId}", ct);
            var p = await promotions.GetDetailsAsync(request.PromotionId, ct)
                ?? throw new NotFoundException(nameof(Promotion), request.PromotionId);
            var session = await sessions.GetOpenByDriverAsync(driverId, ct)
                ?? throw new ConflictException("No tenes una salida abierta.");
            if (p.VehicleId != session.VehicleId)
                throw new ForbiddenException("La promocion pertenece a otro vehiculo.");
            if (p.Status != PromotionStatus.Pending)
                throw new ConflictException("La promocion ya esta cerrada.");
            await promotions.LockAsync($"promotion-vehicle:{session.VehicleId}", ct);
            var now = DateTime.UtcNow;
            if (request.ConvertToCustomer)
            {
                var data = request.Customer ?? new NewCustomerLine(p.BusinessName, p.ContactName, p.Phone,
                    p.Address, p.Notes, p.VisitDays);
                var validation = await new NewCustomerLineValidator().ValidateAsync(data, ct);
                if (!validation.IsValid)
                    throw new GMSoft.Application.Common.Exceptions.ValidationException(validation.Errors);
                // Conserva zona y camion de origen, aunque hoy se reparta otra zona.
                var customer = await StreetCustomer.CreateAsync(data, p.VehicleId, p.ZoneId, customers, ct);
                await uow.SaveChangesAsync(ct);
                foreach (var line in p.Lines.Where(l => l.ContainersLoaned > 0))
                {
                    PromotionMovements.Add(p, line.ProductId, -line.ContainersLoaned, ContainerMovementType.Adjustment,
                        now, user.UserId, "Traspaso del prospecto al cliente");
                    PromotionMovements.Add(p, line.ProductId, line.ContainersLoaned, ContainerMovementType.DeliveredToCustomer,
                        now, user.UserId, "Saldo inicial por conversion", customer.Id);
                    await balances.AdjustAsync(customer.Id, line.ProductId, line.ContainersLoaned, ct);
                }
                p.CustomerId = customer.Id;
                p.Status = PromotionStatus.Converted;
            }
            else
            {
                foreach (var returned in request.ContainersReturned)
                {
                    var line = p.Lines.SingleOrDefault(l => l.ProductId == returned.ProductId);
                    if (line is null || line.ContainersLoaned == 0 || returned.Quantity > line.ContainersLoaned)
                        throw new BadRequestException("El retiro debe corresponder a los envases prestados y no superarlos.");
                }
                foreach (var line in p.Lines.Where(l => l.ContainersLoaned > 0))
                {
                    line.ContainersReturned = request.ContainersReturned.SingleOrDefault(r => r.ProductId == line.ProductId)?.Quantity ?? 0;
                    line.ContainersLost = line.ContainersLoaned - line.ContainersReturned;
                    if (line.ContainersReturned > 0)
                    {
                        PromotionMovements.Add(p, line.ProductId, -line.ContainersReturned,
                            ContainerMovementType.ReturnedFromCustomer, now, user.UserId, "Retiro al prospecto");
                        session.StockMovements.Add(new SessionStockMovement
                        {
                            DeliverySessionId = session.Id, ProductId = line.ProductId, Quantity = line.ContainersReturned,
                            State = ContainerState.Empty, Type = SessionStockMovementType.CollectedEmpty,
                            OccurredAt = now, RegisteredByUserId = user.UserId, Notes = $"Retiro promocion {p.Id}"
                        });
                    }
                    if (line.ContainersLost > 0) PromotionMovements.Add(p, line.ProductId, -line.ContainersLost,
                        ContainerMovementType.Lost, now, user.UserId, "Faltante al cerrar sin conversion");
                }
                p.Status = PromotionStatus.NotConverted;
                sessions.Update(session);
            }
            p.ClosedAt = now;
            p.CloseClientRequestId = request.ClientRequestId;
            p.ClosedByDriverId = driverId;
            p.ClosingSessionId = session.Id;
            promotions.Update(p);
            await uow.SaveChangesAsync(ct);
            result = PromotionResult.From(p);
        }, ct);
        return result;
    }
}

