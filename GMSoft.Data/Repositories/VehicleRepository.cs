using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Application.Features.Vehicles.LoadStatus;
using GMSoft.Data.Context;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.Data.Repositories;

public class VehicleRepository : Repository<Vehicle>, IVehicleRepository
{
    public VehicleRepository(AppDbContext context) : base(context) { }

    public async Task<(IReadOnlyList<Vehicle> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Vehicles.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(v =>
                EF.Functions.ILike(v.Name, $"%{term}%") ||
                EF.Functions.ILike(v.LicensePlate, $"%{term}%"));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(v => v.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<bool> ExistsByLicensePlateAsync(
        string licensePlate,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default)
        => await _context.Vehicles
            .AsNoTracking()
            .AnyAsync(v =>
                v.LicensePlate == licensePlate &&
                (excludeId == null || v.Id != excludeId),
                cancellationToken);

    public async Task<bool> HasHistoryAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.DeliverySessions
            .AsNoTracking()
            .AnyAsync(s => s.VehicleId == id, cancellationToken);

    // Los dos estados salen como subconsultas dentro de la misma proyeccion: una
    // sola ida a la base para toda la flota, en vez de dos por camion.
    public async Task<IReadOnlyList<VehicleLoadStatusDto>> GetLoadStatusAsync(
        CancellationToken cancellationToken = default)
        => await _context.Vehicles
            .AsNoTracking()
            .OrderBy(v => v.Name)
            .Select(v => new VehicleLoadStatusDto(
                v.Id,
                v.Name,
                v.LicensePlate,
                _context.DeliverySessions
                    .Any(s => s.VehicleId == v.Id && s.Status == SessionStatus.Open),
                // Sum sobre vacio devuelve null en SQL: el cast a int? y el ?? 0
                // evitan que un camion sin cargar reviente la consulta.
                _context.VehicleLoads
                    .Where(l => l.VehicleId == v.Id && l.ConsumedBySessionId == null)
                    .Sum(l => (int?)l.Quantity) ?? 0))
            .ToListAsync(cancellationToken);
}
