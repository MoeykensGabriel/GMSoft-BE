using FluentValidation;
using GMSoft.Application.Common;
using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Enums;
using MediatR;

namespace GMSoft.Application.Features.Access;

public record AccessEventRow(Guid DriverId, string DriverName, DateTime OccurredAt, AccessEventKind Kind,
    AccessEndReason? Reason, AccessClient Client, string? Device, Guid? DeviceId);

/// <summary>Por qué una entrada vino sin una salida antes.</summary>
public enum AccessLoss
{
    None = 0,

    /// <summary>Mismo celular, pero la salida no llegó al servidor (por ejemplo, cerró sin señal).</summary>
    NoExitRecorded = 1,

    /// <summary>El celular ya no tenía los datos de la página: los borró, o entró desde otro navegador.</summary>
    DataCleared = 2
}

public record AccessEventDto(DateTime OccurredAt, AccessEventKind Kind, AccessEndReason? Reason, AccessClient Client,
    string? Device, AccessLoss Loss);
public record AccessDriverDto(Guid DriverId, string DriverName, int Logins, int Problems, IReadOnlyList<AccessEventDto> Events);
public record AccessControlDto(DateOnly Day, IReadOnlyList<AccessDriverDto> Drivers);

public record GetAccessControlQuery(DateOnly Day, Guid? DriverId = null) : IRequest<AccessControlDto>;

public class GetAccessControlQueryValidator : AbstractValidator<GetAccessControlQuery>
{
    public GetAccessControlQueryValidator()
        => RuleFor(x => x.Day).GreaterThan(DateOnly.MinValue).LessThan(DateOnly.MaxValue);
}

public class GetAccessControlQueryHandler(IAccessEventRepository repository, ICurrentUserService user)
    : IRequestHandler<GetAccessControlQuery, AccessControlDto>
{
    public async Task<AccessControlDto> Handle(GetAccessControlQuery request, CancellationToken cancellationToken)
    {
        if (!user.IsInRole(AppRoles.Admin)) throw new ForbiddenException("El control de sesiones es solo para ADMIN.");
        var (from, to) = BusinessTime.DayRangeUtc(request.Day);
        var rows = await repository.GetAsync(from, to, request.DriverId, cancellationToken);
        return new(request.Day, AccessControlAnalysis.Build(rows, from));
    }
}

public static class AccessControlAnalysis
{
    /// <summary>
    /// Arma el día de cada chofer. Una entrada sin una salida antes es una sesión perdida:
    /// la app siempre avisa al cerrar, salvo que el celular haya borrado sus datos o no
    /// haya tenido señal para avisar.
    /// </summary>
    public static IReadOnlyList<AccessDriverDto> Build(IEnumerable<AccessEventRow> rows, DateTime fromUtc)
        => rows.GroupBy(r => r.DriverId)
            .Select(group =>
            {
                var events = new List<AccessEventDto>();
                AccessEventRow? previous = null;
                foreach (var row in group.OrderBy(r => r.OccurredAt))
                {
                    var loss = row.Kind == AccessEventKind.Login && previous is { Kind: AccessEventKind.Login }
                        ? previous.DeviceId is { } before && row.DeviceId is { } now && before != now
                            ? AccessLoss.DataCleared : AccessLoss.NoExitRecorded
                        : AccessLoss.None;
                    if (row.OccurredAt >= fromUtc)
                        events.Add(new(row.OccurredAt, row.Kind, row.Reason, row.Client, row.Device, loss));
                    previous = row;
                }
                return new AccessDriverDto(group.Key, group.First().DriverName,
                    events.Count(e => e.Kind == AccessEventKind.Login),
                    events.Count(IsProblem), events);
            })
            .Where(d => d.Events.Count > 0)
            .OrderByDescending(d => d.Problems).ThenBy(d => d.DriverName)
            .ToList();

    /// <summary>Lo que sacó al chofer sin que él ni la oficina lo hayan decidido.</summary>
    public static bool IsProblem(AccessEventDto e)
        => e.Loss != AccessLoss.None || e.Reason is AccessEndReason.Expired;
}
