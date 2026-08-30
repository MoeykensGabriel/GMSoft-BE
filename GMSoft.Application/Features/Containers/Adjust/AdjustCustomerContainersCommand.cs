using MediatR;

namespace GMSoft.Application.Features.Containers.Adjust;

/// <summary>
/// La oficina cuenta lo que el cliente tiene de verdad y corrige el saldo.
/// Se manda la cantidad REAL, no la diferencia: quien cuenta sabe que hay tres
/// bidones, no que sobran dos. La diferencia la calcula el sistema.
/// </summary>
public record AdjustCustomerContainersCommand(
    Guid   CustomerId,
    Guid   ProductId,
    int    RealQuantity,
    string Reason) : IRequest<AdjustCustomerContainersResult>;

public record AdjustCustomerContainersResult(
    int PreviousQuantity,
    int NewQuantity,
    int Delta);
