using GMSoft.Domain.Entities;

namespace GMSoft.Application.Common.Interfaces.Repositories;

public interface IProductRepository : IRepository<Product>
{
    /// <summary>
    /// Página del catálogo. Devuelve los ítems y el total, porque el frontend
    /// necesita saber cuántas páginas hay sin traerlas todas.
    /// </summary>
    Task<(IReadOnlyList<Product> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        bool? onlyPublished,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Si ya se vendio, se movio en un camion o movio envases. Un producto con
    /// historia no se elimina, se despublica: borrarlo lo sacaria de las entregas
    /// viejas y del libro mayor de envases.
    /// </summary>
    Task<bool> HasHistoryAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Para no permitir dos productos con el mismo detalle.</summary>
    Task<bool> ExistsByDetailAsync(
        string detail,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default);
}
