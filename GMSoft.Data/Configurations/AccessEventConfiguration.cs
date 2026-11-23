using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMSoft.Data.Configurations;

public class AccessEventConfiguration : IEntityTypeConfiguration<AccessEvent>
{
    public void Configure(EntityTypeBuilder<AccessEvent> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Device).HasMaxLength(120);
        builder.Property(e => e.TokenId).HasMaxLength(64);
        builder.HasOne<Driver>().WithMany().HasForeignKey(e => e.DriverId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.DriverId, e.OccurredAt });
        // Un token rechazado se anota una vez aunque la app mande varios pedidos a la vez.
        builder.HasIndex(e => new { e.UserId, e.TokenId }).IsUnique().HasFilter("\"TokenId\" IS NOT NULL");
    }
}
