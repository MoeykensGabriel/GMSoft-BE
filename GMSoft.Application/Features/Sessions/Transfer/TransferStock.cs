using FluentValidation;
using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using MediatR;

namespace GMSoft.Application.Features.Sessions.Transfer;

/// <summary>
/// Pase de llenos entre dos camiones en la calle: a uno le faltan para vender y el otro
/// le pasa. Descuenta del que da y suma al que recibe en el mismo momento, así las dos
/// recepciones cuadran solas. Lo registra la oficina, como la recarga.
/// </summary>
public record TransferStockCommand(Guid FromSessionId, Guid ToSessionId, IReadOnlyList<TransferStockItem> Items)
    : IRequest<TransferStockResult>;

public record TransferStockItem(Guid ProductId, int Quantity);

public record TransferStockResult(string FromVehicle, string ToVehicle, IReadOnlyList<TransferStockLine> Lines);

public record TransferStockLine(Guid ProductId, string ProductDetail, int Quantity);

public class TransferStockCommandValidator : AbstractValidator<TransferStockCommand>
{
    public TransferStockCommandValidator()
    {
        RuleFor(x => x.FromSessionId).NotEmpty();
        RuleFor(x => x.ToSessionId).NotEmpty()
            .NotEqual(x => x.FromSessionId).WithMessage("El camión que recibe tiene que ser otro.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("Indicá al menos un producto.");
        RuleForEach(x => x.Items).NotNull().ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).NotEmpty();
            item.RuleFor(i => i.Quantity).GreaterThan(0);
        });
        RuleFor(x => x.Items)
            .Must(items => items is null || items.Where(i => i is not null)
                .Select(i => i.ProductId).Distinct().Count() == items.Count)
            .WithMessage("Hay productos repetidos en el pase.");
    }
}

public class TransferStockCommandHandler(
    ISessionRepository sessions,
    IRepository<SessionStockMovement> movements,
    ICurrentUserService currentUser,
    IUnitOfWork unitOfWork) : IRequestHandler<TransferStockCommand, TransferStockResult>
{
    public async Task<TransferStockResult> Handle(TransferStockCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsInRole(AppRoles.Admin) || currentUser.UserId is null)
            throw new ForbiddenException("Solo el administrador puede registrar pases entre camiones.");

        var from = await sessions.GetWithDetailsAsync(request.FromSessionId, cancellationToken)
            ?? throw new NotFoundException(nameof(DeliverySession), request.FromSessionId);
        var to = await sessions.GetWithDetailsAsync(request.ToSessionId, cancellationToken)
            ?? throw new NotFoundException(nameof(DeliverySession), request.ToSessionId);
        if (from.VehicleId == to.VehicleId)
            throw new BadRequestException("El camión que recibe tiene que ser otro.");

        var lines = new List<TransferStockLine>();
        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // Siempre en el mismo orden: dos pases cruzados al mismo tiempo no se traban.
            foreach (var id in new[] { from.Id, to.Id }.Order())
                if (!await sessions.LockOpenAsync(id, cancellationToken))
                    throw new ConflictException("Los dos camiones tienen que estar en la calle: uno ya fue recibido.");

            // El saldo se lee después del bloqueo: una venta no puede gastar lo mismo en paralelo.
            var onBoard = (await sessions.GetStockBalanceAsync(from.Id, cancellationToken)).ToDictionary(l => l.ProductId);
            var now = DateTime.UtcNow;
            foreach (var item in request.Items)
            {
                if (!onBoard.TryGetValue(item.ProductId, out var line) || line.FullOnBoard < item.Quantity)
                    throw new ConflictException(
                        $"{from.Vehicle.Name} no tiene {item.Quantity} llenos de {line?.ProductDetail ?? "ese producto"} a bordo " +
                        $"(tiene {line?.FullOnBoard ?? 0}).");
                await movements.AddAsync(Movement(from.Id, item, -item.Quantity, SessionStockMovementType.TransferOut,
                    $"Pase a {to.Vehicle.Name}", now), cancellationToken);
                await movements.AddAsync(Movement(to.Id, item, item.Quantity, SessionStockMovementType.TransferIn,
                    $"Pase de {from.Vehicle.Name}", now), cancellationToken);
                lines.Add(new(item.ProductId, line.ProductDetail, item.Quantity));
            }
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }, cancellationToken);

        return new(from.Vehicle.Name, to.Vehicle.Name, lines);
    }

    private SessionStockMovement Movement(Guid sessionId, TransferStockItem item, int quantity,
        SessionStockMovementType type, string notes, DateTime now) => new()
    {
        DeliverySessionId = sessionId, ProductId = item.ProductId, State = ContainerState.Full,
        Quantity = quantity, Type = type, OccurredAt = now, RegisteredByUserId = currentUser.UserId, Notes = notes
    };
}
