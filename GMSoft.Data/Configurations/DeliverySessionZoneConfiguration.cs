using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMSoft.Data.Configurations;

public class DeliverySessionZoneConfiguration : IEntityTypeConfiguration<DeliverySessionZone>
{
    public void Configure(EntityTypeBuilder<DeliverySessionZone> b)
    {
        b.ToTable("DeliverySessionZones", t => t.HasCheckConstraint("CK_DeliverySessionZones_Position", "\"Position\" >= 0"));
        b.HasKey(z => new { z.DeliverySessionId, z.Position });
        // La misma zona no se recorre dos veces en una salida.
        b.HasIndex(z => new { z.DeliverySessionId, z.ZoneId }).IsUnique();
        b.HasIndex(z => z.ZoneId);
        b.HasOne(z => z.Zone).WithMany().HasForeignKey(z => z.ZoneId).OnDelete(DeleteBehavior.Restrict);
    }
}
