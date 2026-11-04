using GMSoft.Application.Common;
using GMSoft.Application.Common.Interfaces.Repositories;
using MediatR;

namespace GMSoft.Application.Features.Sessions.UsageSince;

/// <summary>Desde que dia se usa el sistema: el de la primera salida. Null si todavia no hubo ninguna.</summary>
public record UsageSinceDto(DateOnly? Since, int? Days);

public record GetUsageSinceQuery : IRequest<UsageSinceDto>;

public class GetUsageSinceQueryHandler(ISessionRepository sessions) : IRequestHandler<GetUsageSinceQuery, UsageSinceDto>
{
    public async Task<UsageSinceDto> Handle(GetUsageSinceQuery request, CancellationToken cancellationToken)
        => UsageSince.From(await sessions.GetFirstOpenedAtAsync(cancellationToken), DateTime.UtcNow);
}

public static class UsageSince
{
    /// <summary>El dia de la primera salida cuenta como dia 1, en hora de Argentina.</summary>
    public static UsageSinceDto From(DateTime? firstOpenedUtc, DateTime nowUtc)
    {
        if (firstOpenedUtc is not DateTime first) return new(null, null);
        var since = DateOnly.FromDateTime(first + BusinessTime.Offset);
        var today = DateOnly.FromDateTime(nowUtc + BusinessTime.Offset);
        return new(since, today.DayNumber - since.DayNumber + 1);
    }
}
