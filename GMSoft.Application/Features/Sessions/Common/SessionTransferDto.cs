namespace GMSoft.Application.Features.Sessions.Common;

/// <summary>
/// Un pase entre camiones visto desde esta salida: si recibió o dio, con quién
/// («Pase de Toyota» / «Pase a Toyota») y qué productos.
/// </summary>
public record SessionTransferDto(DateTime OccurredAt, bool Incoming, string Notes, IReadOnlyList<SessionRestockItemDto> Items);
