using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Domain.Common;

namespace GMSoft.Data.Context;

public class AppDbContext : DbContext, IUnitOfWork
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Domain DbSets — se agregan acá a medida que aparecen las entidades.

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Aplicar todas las configuraciones IEntityTypeConfiguration de esta capa
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Global query filter de soft delete para todas las entidades que heredan BaseEntity
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(BaseEntity).IsAssignableFrom(entityType.ClrType)) continue;

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var property  = Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
            var filter    = Expression.Lambda(Expression.Not(property), parameter);

            entityType.SetQueryFilter(filter);
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries<BaseEntity>();

        foreach (var entry in entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.Id        = entry.Entity.Id == Guid.Empty ? Guid.NewGuid() : entry.Entity.Id;
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;

                case EntityState.Deleted:
                    // Interceptar deletes físicos y convertirlos en soft delete
                    entry.State            = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.DeletedAt = DateTime.UtcNow;
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
