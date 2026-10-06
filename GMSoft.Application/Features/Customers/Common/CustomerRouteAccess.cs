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

    public static void EnsureMatchesSession(Customer customer, DeliverySession session)
    {
        if (!customer.IsActive || customer.VehicleId is null || customer.VehicleId != session.VehicleId
            || customer.ZoneId != session.ZoneId
            || customer.VisitDays is null || !customer.VisitDays.Intersect(Days(session)).Any())
            throw new BadRequestException("El cliente no pertenece al camión, zona y días de esta salida.");
    }
}
