using Microsoft.EntityFrameworkCore;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Data.Context;
using GMSoft.Domain.Common;

namespace GMSoft.Data.Repositories;

public class Repository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly AppDbContext _context;

    public Repository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Set<T>().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.Set<T>().ToListAsync(cancellationToken);

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
        => await _context.Set<T>().AddAsync(entity, cancellationToken);

    /// <summary>
    /// Marca SOLO la entidad raíz como Modified para que SaveChangesAsync actualice UpdatedAt.
    /// No toca las entidades relacionadas — sus estados (Added/Unchanged) ya son correctos en
    /// el ChangeTracker. Usar _context.Update(entity) recorrería todo el grafo y re-marcaría
    /// como Modified entradas recién agregadas, causando DbUpdateConcurrencyException.
    /// </summary>
    public void Update(T entity)
        => _context.Entry(entity).State = EntityState.Modified;

    /// <summary>
    /// Llama a Remove() pero SaveChangesAsync lo intercepta y convierte en soft delete.
    /// Nunca hay un DELETE físico en la DB.
    /// </summary>
    public void Delete(T entity)
        => _context.Set<T>().Remove(entity);

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Set<T>().AnyAsync(e => e.Id == id, cancellationToken);
}
