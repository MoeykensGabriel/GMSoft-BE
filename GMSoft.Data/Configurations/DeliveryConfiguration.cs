using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMSoft.Data.Configurations;

public class DeliveryConfiguration : IEntityTypeConfiguration<Delivery>
{
    public void Configure(EntityTypeBuilder<Delivery> builder)
    {
        builder.ToTable("Deliveries");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Total).HasPrecision(18, 2);
        builder.Property(d => d.Notes).HasMaxLength(1000);

        // El estado de cuenta de un cliente recorre sus entregas por fecha.
        builder.HasIndex(d => new { d.CustomerId, d.DeliveredAt });

        builder.HasOne(d => d.DeliverySession)
               .WithMany(s => s.Deliveries)
               .HasForeignKey(d => d.DeliverySessionId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Customer)
               .WithMany()
               .HasForeignKey(d => d.CustomerId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
