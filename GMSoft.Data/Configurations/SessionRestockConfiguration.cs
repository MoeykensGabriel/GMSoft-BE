using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMSoft.Data.Configurations;

public class SessionRestockConfiguration : IEntityTypeConfiguration<SessionRestock>
{
    public void Configure(EntityTypeBuilder<SessionRestock> builder)
    {
        builder.ToTable("SessionRestocks");
        builder.HasKey(r => r.Id);
        builder.HasIndex(r => r.ClientRequestId).IsUnique();
        builder.HasIndex(r => new { r.DeliverySessionId, r.OccurredAt });
        builder.Property(r => r.Notes).HasMaxLength(500);
        builder.HasOne(r => r.DeliverySession).WithMany(s => s.Restocks)
            .HasForeignKey(r => r.DeliverySessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(r => r.Items).WithOne(m => m.SessionRestock)
            .HasForeignKey(m => m.SessionRestockId).OnDelete(DeleteBehavior.Restrict);
    }
}
