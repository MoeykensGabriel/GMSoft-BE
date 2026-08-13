namespace GMSoft.Domain.Common;

/// <summary>
/// Base de toda entidad del dominio. El Id y las fechas los completa
/// AppDbContext.SaveChangesAsync — nunca se asignan a mano.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
