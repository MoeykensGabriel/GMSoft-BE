using MediatR;

namespace GMSoft.Application.Features.Containers.Loss;

/// <summary>
/// Envases que el cliente tenia y no vuelven: rotos, perdidos o que no se van a
/// reclamar mas. Distinto de un ajuste, aunque los dos bajen el saldo: el ajuste
/// dice que el conteo estaba mal, esto dice que existian y se perdieron. La
/// diferencia importa para saber cuanto se pierde por año.
/// </summary>
public record RegisterContainerLossCommand(
    Guid   CustomerId,
    Guid   ProductId,
    int    Quantity,
    string Reason) : IRequest;
