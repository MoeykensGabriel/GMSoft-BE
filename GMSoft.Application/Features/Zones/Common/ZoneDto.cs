namespace GMSoft.Application.Features.Zones.Common;

public record ZoneDto(
    Guid    Id,
    string  Name,
    string? Notes,
    bool    IsActive);
