using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Data.Identity;
using GMSoft.Domain.Common;
using GMSoft.Domain.Entities;

namespace GMSoft.Data.Context;

public class AppDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>, IUnitOfWork
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Catálogo y personas
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Zone> Zones => Set<Zone>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerProductPrice> CustomerProductPrices => Set<CustomerProductPrice>();

    // Reparto
    public DbSet<DeliverySession> DeliverySessions => Set<DeliverySession>();
    public DbSet<SessionStockMovement> SessionStockMovements => Set<SessionStockMovement>();
    public DbSet<VehicleLoad> VehicleLoads => Set<VehicleLoad>();
    public DbSet<Delivery> Deliveries => Set<Delivery>();
    public DbSet<DeliveryItem> DeliveryItems => Set<DeliveryItem>();

    // Envases
    public DbSet<ContainerMovement> ContainerMovements => Set<ContainerMovement>();
    public DbSet<CustomerContainerBalance> CustomerContainerBalances => Set<CustomerContainerBalance>();
    public DbSet<ContainerUnit> ContainerUnits => Set<ContainerUnit>();

    // Cobranza
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<SessionCashSettlement> SessionCashSettlements => Set<SessionCashSettlement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Aplicar todas las configuraciones IEntityTypeConfiguration de esta capa
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Global query filter de soft delete para todas las entidades que heredan BaseEntity.
        // Las tablas de Identity no heredan de BaseEntity, así que quedan afuera.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(BaseEntity).IsAssignableFrom(entityType.ClrType)) continue;

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var property  = Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
            var filter    = Expression.Lambda(Expression.Not(property), parameter);

            entityType.SetQueryFilter(filter);
        }

        SeedRoles(modelBuilder);
    }

    /// <summary>
    /// Los roles van sembrados con Guid y ConcurrencyStamp fijos. Si fueran
    /// generados, cada migración detectaría un cambio y volvería a escribirlos.
    /// </summary>
    private static void SeedRoles(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationRole>().HasData(
            new ApplicationRole
            {
                Id               = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Name             = AppRoles.Admin,
                NormalizedName   = AppRoles.Admin.ToUpperInvariant(),
                ConcurrencyStamp = "b1f1b1c0-0000-0000-0000-000000000001"
            },
            new ApplicationRole
            {
                Id               = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                Name             = AppRoles.Driver,
                NormalizedName   = AppRoles.Driver.ToUpperInvariant(),
                ConcurrencyStamp = "b1f1b1c0-0000-0000-0000-000000000002"
            });
    }

    /// <summary>
    /// Envuelve la operacion en una transaccion, con la estrategia de reintentos de
    /// Npgsql. La estrategia es necesaria: sin ella, un reintento sobre una
    /// transaccion manual falla en vez de reintentar.
    /// </summary>
    public async Task ExecuteInTransactionAsync(
        Func<Task> action,
        CancellationToken cancellationToken = default)
    {
        var strategy = Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
            await action();
            await transaction.CommitAsync(cancellationToken);
        });
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
