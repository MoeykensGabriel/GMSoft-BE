using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMSoft.Data.Configurations;

public class DeliveryItemConfiguration : IEntityTypeConfiguration<DeliveryItem>
{
    public void Configure(EntityTypeBuilder<DeliveryItem> builder)
    {
        builder.ToTable("DeliveryItems");
        builder.HasKey(i => i.Id);

        // Precio congelado al momento de la venta.
        builder.Property(i => i.UnitPrice).HasPrecision(18, 2);

        // La linea es parte de la entrega, no existe sin ella.
        builder.HasOne(i => i.Delivery)
               .WithMany(d => d.Items)
               .HasForeignKey(i => i.DeliveryId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Product)
               .WithMany()
               .HasForeignKey(i => i.ProductId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
