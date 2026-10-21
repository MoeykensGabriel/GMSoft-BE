using GMSoft.Application.Common;
using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;

namespace GMSoft.Application.Features.Customers.Common;

/// <summary>El alcance del chofer proviene de su salida, nunca de parámetros del navegador.</summary>
public sealed class CustomerRouteAccess(ICurrentUserService user, ISessionRepository sessions)
{
    public async Task<DeliverySession?> GetDriverSessionAsync(CancellationToken cancellationToken)
    {
        if (user.IsInRole(AppRoles.Admin)) return null;
        var driverId = user.DriverId ?? throw new ForbiddenException("Solo admin o chofer pueden consultar clientes.");
        return await sessions.GetOpenByDriverAsync(driverId, cancellationToken)
            ?? throw new ConflictException("No tenés una salida abierta.");
    }

    public async Task EnsureCanReadAsync(Customer customer, CancellationToken cancellationToken)
    {
        var session = await GetDriverSessionAsync(cancellationToken);
        if (session is not null) EnsureMatchesSession(customer, session);
    }

    public static int[] Days(DeliverySession session) => session.RouteDays is { Length: > 0 }
        ? session.RouteDays : [BusinessTime.IsoDayOfWeek(session.OpenedAt)];

    /// <summary>
    /// Las zonas de la salida en el orden en que las recorre. Si no vinieron cargadas
    /// queda solo la principal: se restringe de más, nunca de menos.
    /// </summary>
    public static Guid[] ZoneIds(DeliverySession session) => session.Zones.Count > 0
        ? session.Zones.OrderBy(z => z.Position).Select(z => z.ZoneId).ToArray() : [session.ZoneId];

    /// <summary>
    /// La zona para lo que el chofer da de alta en la calle: la que eligió, si la salida
    /// la recorre; sin elegir, la principal.
    /// </summary>
    public static Guid ResolveZone(DeliverySession session, Guid? requested)
    {
        if (requested is not Guid zoneId) return session.ZoneId;
        if (!ZoneIds(session).Contains(zoneId))
            throw new BadRequestException("La zona elegida no es una de las de esta salida.");
        return zoneId;
    }

    public static void EnsureMatchesSession(Customer customer, DeliverySession session)
    {
        if (!customer.IsActive || customer.VehicleId is null || customer.VehicleId != session.VehicleId
            || !ZoneIds(session).Contains(customer.ZoneId)
            || customer.VisitDays is null || !customer.VisitDays.Intersect(Days(session)).Any())
            throw new BadRequestException("El cliente no pertenece al camión, zonas y días de esta salida.");
    }
}
