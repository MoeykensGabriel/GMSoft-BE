using FluentValidation;
using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Sessions.Common;
using GMSoft.Application.Features.WorkingHours;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using MediatR;

namespace GMSoft.Application.Features.Sessions.Correction;

/// <summary>
/// Corregir la recepcion de una salida ya cerrada, cuando se anoto mal algo al recibir
/// el camion. Cada dato es opcional: va solo lo que se corrige. Lo descargado se
/// manda como el total que de verdad volvio, no como diferencia: es lo que la oficina
/// tiene anotado en la planilla.
/// </summary>
public record CorrectReceptionCommand(
    Guid Id,
    string Reason,
    int? KilometersAtClose,
    decimal? AmountReceived,
    IReadOnlyList<CorrectReturnLine>? Returns) : IRequest<ReceptionDto>;

/// <param name="Quantity">Cuanto volvio en total de ese producto y estado. Cero es valido.</param>
public record CorrectReturnLine(Guid ProductId, ContainerState State, int Quantity);

/// <summary>La recepcion como quedo, con lo descargado por producto y todas sus correcciones.</summary>
public record GetReceptionQuery(Guid Id) : IRequest<ReceptionDto>;

public record ReceptionDto(
    Guid SessionId,
    int KilometersAtOpen,
    int? KilometersAtClose,
    decimal? AmountReceived,
    IReadOnlyList<ReceptionProductDto> Products,
    IReadOnlyList<ReceptionCorrectionDto> Corrections);

/// <summary>Lo descargado y lo que sigue faltando (positivo) o sobrando (negativo) de un producto.</summary>
public record ReceptionProductDto(
    Guid ProductId,
    string ProductDetail,
    int FullReturned,
    int EmptyReturned,
    int FullMissing,
    int EmptyMissing);

public record ReceptionCorrectionDto(
    Guid Id,
    DateTime CorrectedAt,
    string? CorrectedByUserName,
    string Reason,
    int? PreviousKilometers,
    int? NewKilometers,
    decimal? PreviousAmountReceived,
    decimal? NewAmountReceived,
    IReadOnlyList<ReceptionCorrectionLineDto> Lines);

public record ReceptionCorrectionLineDto(
    Guid ProductId,
    string ProductDetail,
    ContainerState State,
    int PreviousQuantity,
    int NewQuantity);

/// <summary>Lo descargado de un producto y estado, sumando la descarga original y sus correcciones.</summary>
public record ReturnedQuantity(Guid ProductId, ContainerState State, int Quantity);

public interface IReceptionCorrectionRepository
{
    /// <summary>Lo descargado al cerrar, con las correcciones ya aplicadas, en positivo.</summary>
    Task<IReadOnlyList<ReturnedQuantity>> GetReturnedAsync(Guid sessionId, CancellationToken ct);

    /// <summary>
    /// El kilometraje con que salio el mismo vehiculo despues de esta salida, si volvio a
    /// salir. El de vuelta corregido no puede pasarlo.
    /// </summary>
    Task<int?> GetNextOpeningKilometersAsync(Guid vehicleId, Guid sessionId, DateTime openedAt, CancellationToken ct);

    Task<IReadOnlyList<SessionReceptionCorrection>> GetHistoryAsync(Guid sessionId, CancellationToken ct);

    Task AddAsync(SessionReceptionCorrection correction, CancellationToken ct);
}

public class CorrectReceptionCommandValidator : AbstractValidator<CorrectReceptionCommand>
{
    // Topes contra un número tipeado de más (un cero de sobra), no reglas del negocio.
    public const int MaxQuantity = 1000;
    public const decimal MaxAmount = 100_000_000m;

    public CorrectReceptionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Reason)
            .Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage("Escribí el motivo de la corrección.")
            .MaximumLength(500);
        RuleFor(x => x.KilometersAtClose)
            .GreaterThanOrEqualTo(0).WithMessage("El kilometraje no puede ser negativo.");
        RuleFor(x => x.AmountReceived)
            .GreaterThanOrEqualTo(0).WithMessage("La plata rendida no puede ser negativa.")
            .LessThanOrEqualTo(MaxAmount);

        RuleForEach(x => x.Returns).ChildRules(linea =>
        {
            linea.RuleFor(l => l.ProductId).NotEmpty();
            linea.RuleFor(l => l.State).IsInEnum();
            linea.RuleFor(l => l.Quantity)
                .GreaterThanOrEqualTo(0).WithMessage("Lo descargado no puede ser negativo.")
                .LessThanOrEqualTo(MaxQuantity);
        });
        RuleFor(x => x.Returns)
            .Must(r => r is null || r.Select(l => (l.ProductId, l.State)).Distinct().Count() == r.Count)
            .WithMessage("Hay líneas repetidas en la descarga.");

        RuleFor(x => x)
            .Must(x => x.KilometersAtClose is not null || x.AmountReceived is not null || x.Returns is { Count: > 0 })
            .WithMessage("No hay nada para corregir.");
    }
}

public class ReceptionCorrectionHandlers(
    ISessionRepository sessions,
    IReceptionCorrectionRepository corrections,
    IVehicleRepository vehicles,
    IIdentityService identity,
    ICurrentUserService currentUser,
    IUnitOfWork unitOfWork,
    WorkingHoursService workingHours)
    : IRequestHandler<CorrectReceptionCommand, ReceptionDto>, IRequestHandler<GetReceptionQuery, ReceptionDto>
{
    public async Task<ReceptionDto> Handle(CorrectReceptionCommand request, CancellationToken cancellationToken)
    {
        // El endpoint ya es solo de admin; esto lo respalda del lado de la aplicacion.
        if (!currentUser.IsInRole(AppRoles.Admin))
            throw new ForbiddenException("La recepción la corrige la oficina.");

        var ahora = (await workingHours.EvaluateAsync(cancellationToken)).UtcNow;
        var motivo = request.Reason.Trim();

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // El mismo bloqueo que toman la recepcion y la rendicion: no se corrige sobre algo a medio cerrar.
            var session = await sessions.LockAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException(nameof(DeliverySession), request.Id);
            if (session.Status != SessionStatus.Closed)
                throw new ConflictException("La salida todavía no se recibió: se corrige después de recibir el camión.");

            var correction = new SessionReceptionCorrection
            {
                DeliverySessionId = session.Id,
                CorrectedAt = ahora,
                CorrectedByUserId = currentUser.UserId,
                Reason = motivo
            };

            if (request.KilometersAtClose is int km && km != session.KilometersAtClose)
                await CorrectKilometers(session, km, correction, cancellationToken);

            if (request.AmountReceived is decimal monto)
            {
                var settlement = await sessions.GetSettlementAsync(session.Id, cancellationToken)
                    ?? throw new BadRequestException(
                        "Esta salida todavía no se rindió: cargá la plata desde la liquidación.");
                if (settlement.AmountReceived != monto)
                {
                    correction.PreviousAmountReceived = settlement.AmountReceived;
                    correction.NewAmountReceived = monto;
                    settlement.AmountReceived = monto;
                }
            }

            if (request.Returns is { Count: > 0 })
                await CorrectReturns(session, request.Returns, correction, motivo, ahora, cancellationToken);

            if (correction.NewKilometers is null && correction.NewAmountReceived is null && correction.Lines.Count == 0)
                throw new BadRequestException("No hay nada para corregir: los datos son iguales a los que ya estaban.");

            await corrections.AddAsync(correction, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }, cancellationToken);

        return await Handle(new GetReceptionQuery(request.Id), cancellationToken);
    }

    private async Task CorrectKilometers(DeliverySession session, int km, SessionReceptionCorrection correction,
        CancellationToken cancellationToken)
    {
        if (km < session.KilometersAtOpen)
            throw new BadRequestException(
                $"El kilometraje de vuelta no puede ser menor al de salida ({session.KilometersAtOpen} km).");

        var siguiente = await corrections.GetNextOpeningKilometersAsync(
            session.VehicleId, session.Id, session.OpenedAt, cancellationToken);
        if (siguiente is int proxima && km > proxima)
            throw new BadRequestException(
                $"El vehículo volvió a salir con {proxima} km: el de vuelta no puede ser mayor.");

        // Si fue la ultima salida del vehiculo, el kilometraje actual era el de esta vuelta.
        if (siguiente is null)
        {
            var vehicle = await vehicles.GetByIdAsync(session.VehicleId, cancellationToken)
                ?? throw new NotFoundException(nameof(Vehicle), session.VehicleId);
            if (vehicle.CurrentKilometers == session.KilometersAtClose)
            {
                vehicle.CurrentKilometers = km;
                vehicles.Update(vehicle);
            }
        }

        correction.PreviousKilometers = session.KilometersAtClose;
        correction.NewKilometers = km;
        session.KilometersAtClose = km;
        sessions.Update(session);
    }

    private async Task CorrectReturns(DeliverySession session, IReadOnlyList<CorrectReturnLine> lineas,
        SessionReceptionCorrection correction, string motivo, DateTime ahora, CancellationToken cancellationToken)
    {
        // Solo productos que anduvieron en esta salida: otro no pudo haber vuelto en el camion.
        var deLaSalida = (await sessions.GetStockBalanceAsync(session.Id, cancellationToken))
            .Select(l => l.ProductId).ToHashSet();
        var ajeno = lineas.FirstOrDefault(l => !deLaSalida.Contains(l.ProductId));
        if (ajeno is not null)
            throw new BadRequestException("Uno de los productos no anduvo en esta salida.");

        var descargado = (await corrections.GetReturnedAsync(session.Id, cancellationToken))
            .ToDictionary(r => (r.ProductId, r.State), r => r.Quantity);

        foreach (var linea in lineas.OrderBy(l => l.ProductId).ThenBy(l => l.State))
        {
            var anterior = descargado.GetValueOrDefault((linea.ProductId, linea.State));
            var diferencia = linea.Quantity - anterior;
            if (diferencia == 0) continue;

            // Un movimiento mas de descarga: el faltante se recalcula solo y nada se borra.
            session.StockMovements.Add(new SessionStockMovement
            {
                DeliverySessionId = session.Id,
                ProductId = linea.ProductId,
                State = linea.State,
                Quantity = -diferencia,
                Type = SessionStockMovementType.ReturnedAtClose,
                OccurredAt = ahora,
                RegisteredByUserId = currentUser.UserId,
                Notes = $"Corrección de recepción: {motivo}"
            });
            correction.Lines.Add(new SessionReceptionCorrectionLine
            {
                ProductId = linea.ProductId,
                State = linea.State,
                PreviousQuantity = anterior,
                NewQuantity = linea.Quantity
            });
        }
    }

    public async Task<ReceptionDto> Handle(GetReceptionQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsInRole(AppRoles.Admin))
            throw new ForbiddenException("La recepción la ve la oficina.");

        var session = await sessions.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(DeliverySession), request.Id);

        var saldo = await sessions.GetStockBalanceAsync(session.Id, cancellationToken);
        var descargado = (await corrections.GetReturnedAsync(session.Id, cancellationToken))
            .ToDictionary(r => (r.ProductId, r.State), r => r.Quantity);
        var settlement = await sessions.GetSettlementAsync(session.Id, cancellationToken);
        var historial = await corrections.GetHistoryAsync(session.Id, cancellationToken);
        var nombres = await identity.GetUserNamesAsync(
            historial.Where(c => c.CorrectedByUserId.HasValue).Select(c => c.CorrectedByUserId!.Value).Distinct().ToArray(),
            cancellationToken);

        var productos = saldo.Select(l => new ReceptionProductDto(
                l.ProductId, l.ProductDetail,
                descargado.GetValueOrDefault((l.ProductId, ContainerState.Full)),
                descargado.GetValueOrDefault((l.ProductId, ContainerState.Empty)),
                l.FullOnBoard, l.EmptyOnBoard))
            .ToList();

        var correcciones = historial.Select(c => new ReceptionCorrectionDto(
                c.Id, c.CorrectedAt,
                c.CorrectedByUserId is Guid id && nombres.TryGetValue(id, out var nombre) ? nombre : null,
                c.Reason, c.PreviousKilometers, c.NewKilometers, c.PreviousAmountReceived, c.NewAmountReceived,
                c.Lines.OrderBy(l => l.Product.Detail).ThenBy(l => l.State)
                    .Select(l => new ReceptionCorrectionLineDto(l.ProductId, l.Product.Detail, l.State,
                        l.PreviousQuantity, l.NewQuantity))
                    .ToList()))
            .ToList();

        return new ReceptionDto(session.Id, session.KilometersAtOpen, session.KilometersAtClose,
            settlement?.AmountReceived, productos, correcciones);
    }
}
