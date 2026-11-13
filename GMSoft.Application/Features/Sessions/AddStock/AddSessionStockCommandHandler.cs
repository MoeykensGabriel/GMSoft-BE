using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Sessions.Common;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using MediatR;
using GMSoft.Application.Features.WorkingHours;

namespace GMSoft.Application.Features.Sessions.AddStock;

public class AddSessionStockCommandHandler(
    ISessionRepository sessions,
    ISessionRestockRepository restocks,
    IRepository<Product> products,
    ICurrentUserService currentUser,
    IUnitOfWork unitOfWork, WorkingHoursService workingHours) : IRequestHandler<AddSessionStockCommand, SessionRestockDto>
{
    public async Task<SessionRestockDto> Handle(AddSessionStockCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsInRole(AppRoles.Admin) || currentUser.UserId is null)
            throw new ForbiddenException("Solo el administrador puede registrar recargas.");

        // Recupera la respuesta incluso despues de la recepcion; no crea otra recarga.
        if (await restocks.GetByClientRequestAsync(request.ClientRequestId, cancellationToken) is { } previous)
            return Original(previous, request.Id);

        var session = await sessions.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(DeliverySession), request.Id);
        if (session.Status != SessionStatus.Open)
            throw new ConflictException("La sesion ya esta cerrada. Una recarga posterior cambiaria el faltante ya informado.");

        var resolved = new Dictionary<Guid, Product>();
        foreach (var item in request.Items)
        {
            var product = await products.GetByIdAsync(item.ProductId, cancellationToken)
                ?? throw new NotFoundException(nameof(Product), item.ProductId);
            if (!product.IsPublished)
                throw new BadRequestException($"'{product.Detail}' no esta publicado para el reparto.");
            if (item.RefilledEmpties > 0 && product.Tracking == ContainerTracking.None)
                throw new BadRequestException($"'{product.Detail}' no tiene envase: no hay vacios que rellenar.");
            // La carga inicial no rechaza ByUnit; mantenemos la misma regla aqui.
            resolved.Add(product.Id, product);
        }

        // La hora y la marca se asignan despues del bloqueo y la busqueda del reintento.
        var restock = new SessionRestock
        {
            DeliverySessionId = session.Id,
            ClientRequestId = request.ClientRequestId,
            RegisteredByUserId = currentUser.UserId.Value,
            Notes = request.Notes?.Trim()
        };
        foreach (var item in request.Items)
            restock.Items.Add(new SessionStockMovement
            {
                DeliverySessionId = session.Id,
                ProductId = item.ProductId,
                RestockProductDetail = resolved[item.ProductId].Detail,
                State = ContainerState.Full,
                Quantity = item.Quantity,
                Type = SessionStockMovementType.Restock,
                RegisteredByUserId = restock.RegisteredByUserId,
                Notes = restock.Notes
            });
        // Los vacios del camion que se llenaron: bajan los vacios en la misma tanda.
        foreach (var item in request.Items.Where(i => i.RefilledEmpties > 0))
            restock.Items.Add(new SessionStockMovement
            {
                DeliverySessionId = session.Id,
                ProductId = item.ProductId,
                RestockProductDetail = resolved[item.ProductId].Detail,
                State = ContainerState.Empty,
                Quantity = -item.RefilledEmpties,
                Type = SessionStockMovementType.RefilledEmpty,
                RegisteredByUserId = restock.RegisteredByUserId,
                Notes = restock.Notes
            });

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                if (!await restocks.IsSessionOpenForUpdateAsync(session.Id, cancellationToken))
                {
                    // Una recarga simultanea pudo terminar antes de este cierre.
                    if (await restocks.GetByClientRequestAsync(request.ClientRequestId, cancellationToken) is { } saved)
                    {
                        Original(saved, request.Id);
                        restock = saved;
                        return;
                    }
                    throw new ConflictException("La sesion ya esta cerrada. No se puede registrar la recarga.");
                }
                if (await restocks.GetByClientRequestAsync(request.ClientRequestId, cancellationToken) is { } winner)
                {
                    Original(winner, request.Id);
                    restock = winner;
                    return;
                }
                // Con la salida bloqueada: no se rellenan vacios que el camion no tiene.
                if (request.Items.Any(i => i.RefilledEmpties > 0))
                {
                    var stock = (await sessions.GetStockBalanceAsync(session.Id, cancellationToken))
                        .ToDictionary(l => l.ProductId, l => l.EmptyOnBoard);
                    foreach (var item in request.Items.Where(i => i.RefilledEmpties > 0))
                    {
                        var onBoard = stock.GetValueOrDefault(item.ProductId);
                        if (item.RefilledEmpties > onBoard)
                            throw new ConflictException(
                                $"El camion tiene {Math.Max(onBoard, 0)} vacios de '{resolved[item.ProductId].Detail}': no se pueden rellenar {item.RefilledEmpties}.");
                    }
                }
                var horario = await workingHours.EnsureAllowedAsync(false, request.ConfirmOutsideWorkingHours, currentUser, cancellationToken);
                restock.OccurredAt = horario.UtcNow;
                restock.IsOutsideWorkingHours = horario.IsOutsideWorkingHours;
                foreach (var item in restock.Items) item.OccurredAt = horario.UtcNow;
                await restocks.AddAsync(restock, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }, cancellationToken);
        }
        catch (DuplicateRestockRequestException)
        {
            // La transaccion perdedora ya hizo rollback; se lee la ganadora.
            var original = await restocks.GetByClientRequestAsync(request.ClientRequestId, cancellationToken);
            if (original is null) throw;
            return Original(original, request.Id);
        }
        return SessionRestockDto.From(restock);
    }

    private static SessionRestockDto Original(SessionRestock restock, Guid sessionId)
    {
        if (restock.DeliverySessionId != sessionId)
            throw new ConflictException("El clientRequestId ya pertenece a una recarga de otra salida.");
        return SessionRestockDto.From(restock);
    }
}
