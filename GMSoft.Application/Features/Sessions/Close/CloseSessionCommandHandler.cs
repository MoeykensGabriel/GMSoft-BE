using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using MediatR;
using GMSoft.Application.Features.Sessions.Common;
using GMSoft.Application.Features.WorkingHours;

namespace GMSoft.Application.Features.Sessions.Close;

public class CloseSessionCommandHandler : IRequestHandler<CloseSessionCommand, CloseSessionResult>
{
    private readonly ISessionRepository _sessions;
    private readonly IVehicleRepository _vehicles;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly WorkingHoursService _workingHours;
    private readonly IProductRepository _products;
    private readonly IRepository<SessionCashSettlement> _settlements;

    public CloseSessionCommandHandler(
        ISessionRepository sessions,
        IVehicleRepository vehicles,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        WorkingHoursService workingHours,
        IProductRepository products,
        IRepository<SessionCashSettlement> settlements)
    {
        _settlements = settlements;
        _sessions    = sessions;
        _vehicles    = vehicles;
        _currentUser = currentUser;
        _unitOfWork  = unitOfWork;
        _workingHours = workingHours;
        _products = products;
    }

    public async Task<CloseSessionResult> Handle(
        CloseSessionCommand request,
        CancellationToken cancellationToken)
    {
        var session = await _sessions.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(DeliverySession), request.Id);

        if (session.Status == SessionStatus.Closed)
            throw new ConflictException("Esta sesion ya esta cerrada.");

        // La recepcion la hace la oficina, no el que trae el camion: si contara el
        // mismo chofer, el control seria una copia de lo que el ya dijo. El endpoint
        // ya es solo de admin; esto lo respalda del lado de la aplicacion.
        if (!_currentUser.IsInRole(AppRoles.Admin))
            throw new ForbiddenException(
                "El control de recepcion lo hace la oficina cuando vuelve el camion.");

        if (request.KilometersAtClose < session.KilometersAtOpen)
            throw new BadRequestException(
                $"El kilometraje de cierre no puede ser menor al de salida ({session.KilometersAtOpen} km).");

        var vehicle = await _vehicles.GetByIdAsync(session.VehicleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Vehicle), session.VehicleId);

        var productIds = request.Returns.Select(r => r.ProductId).Distinct().ToArray();
        var existingIds = await _products.GetExistingIdsAsync(productIds, cancellationToken);
        var missingIds = productIds.Except(existingIds).ToArray();
        if (missingIds.Length > 0)
            throw new NotFoundException(nameof(Product), missingIds[0]);

        var horario = await _workingHours.EvaluateAsync(cancellationToken);
        var ahora   = horario.UtcNow;
        var usuario = _currentUser.UserId;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            session = await _sessions.LockAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException(nameof(DeliverySession), request.Id);
            if (session.Status != SessionStatus.Open)
                throw new ConflictException("Esta sesion ya esta cerrada.");

            foreach (var linea in request.Returns)
            {
                session.StockMovements.Add(new SessionStockMovement
                {
                    DeliverySessionId  = session.Id,
                    ProductId          = linea.ProductId,
                    State              = linea.State,
                    Quantity           = -linea.Quantity,  // sale del camion al deposito
                    Type               = SessionStockMovementType.ReturnedAtClose,
                    OccurredAt         = ahora,
                    RegisteredByUserId = usuario
                });
            }

            session.ClosedAt          = ahora;
            session.ClosedOutsideWorkingHours = horario.IsOutsideWorkingHours;
            session.ClosedByUserId = usuario;
            session.KilometersAtClose = request.KilometersAtClose;
            session.Status            = SessionStatus.Closed;

            _sessions.Update(session);

            vehicle.CurrentKilometers = request.KilometersAtClose;
            _vehicles.Update(vehicle);

            // La rendicion en la misma transaccion: o queda todo guardado o nada.
            if (request.Settlement is not null)
                await _settlements.AddAsync(new SessionCashSettlement
                {
                    DeliverySessionId     = session.Id,
                    AmountReceived        = request.Settlement.AmountReceived,
                    ReceivedAt            = ahora,
                    IsOutsideWorkingHours = horario.IsOutsideWorkingHours,
                    ReceivedByUserId      = usuario,
                    Notes                 = string.IsNullOrWhiteSpace(request.Settlement.Notes)
                        ? null : request.Settlement.Notes.Trim()
                }, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }, cancellationToken);

        // Lo que quede a bordo despues de descargar es el faltante. No se guarda en
        // ningun campo: es el saldo del libro mayor, y se puede recorrer movimiento
        // por movimiento para ver donde se fue.
        var saldo = await _sessions.GetStockBalanceAsync(session.Id, cancellationToken);

        var faltante = saldo
            .Where(l => l.FullOnBoard != 0 || l.EmptyOnBoard != 0)
            .ToList();

        decimal? diferencia = null;
        if (request.Settlement is not null)
        {
            var cobros = await _sessions.GetPaymentsByCustomerAsync(session.Id, cancellationToken);
            var efectivo = cobros.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount);
            diferencia = CashSettlementCalculation.Difference(efectivo, request.Settlement.AmountReceived);
        }

        return new CloseSessionResult(
            SessionId:  session.Id,
            CuadraTodo: faltante.Count == 0,
            Faltante:   faltante) { CashDifference = diferencia };
    }
}
