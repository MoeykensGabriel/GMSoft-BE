using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMSoft.Data.Configurations;

public class SessionStockMovementConfiguration : IEntityTypeConfiguration<SessionStockMovement>
{
    public void Configure(EntityTypeBuilder<SessionStockMovement> builder)
    {
        builder.ToTable("SessionStockMovements");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Type).HasConversion<int>();
        builder.Property(m => m.State).HasConversion<int>();
        builder.Property(m => m.Notes).HasMaxLength(500);

        // El stock a bordo se calcula sumando por sesion, producto y estado.
        // Sin este indice, cada consulta de stock del camion recorre la tabla entera.
        builder.HasIndex(m => new { m.DeliverySessionId, m.ProductId, m.State });

        // Es un libro mayor: se borra con la sesion y con nada mas.
        builder.HasOne(m => m.DeliverySession)
               .WithMany(s => s.StockMovements)
               .HasForeignKey(m => m.DeliverySessionId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Product)
               .WithMany()
               .HasForeignKey(m => m.ProductId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Delivery)
               .WithMany()
               .HasForeignKey(m => m.DeliveryId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
