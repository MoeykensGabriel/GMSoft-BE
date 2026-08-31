using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMSoft.Data.Configurations;

public class VehicleLoadConfiguration : IEntityTypeConfiguration<VehicleLoad>
{
    public void Configure(EntityTypeBuilder<VehicleLoad> builder)
    {
        builder.ToTable("VehicleLoads");
        builder.HasKey(l => l.Id);

        // La consulta de todos los dias es "que tiene cargado este camion sin salir".
        builder.HasIndex(l => new { l.VehicleId, l.ConsumedBySessionId });

        builder.HasOne(l => l.Vehicle)
               .WithMany(v => v.PendingLoads)
               .HasForeignKey(l => l.VehicleId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Product)
               .WithMany()
               .HasForeignKey(l => l.ProductId)
               .OnDelete(DeleteBehavior.Restrict);

        // Restrict y no Cascade: si se borrara la sesion, la carga volveria a figurar
        // como pendiente y el camion aparecería cargado con algo que ya salio.
        builder.HasOne(l => l.ConsumedBySession)
               .WithMany()
               .HasForeignKey(l => l.ConsumedBySessionId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
