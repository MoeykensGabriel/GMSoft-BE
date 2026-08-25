using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMSoft.Data.Configurations;

public class SessionLoadItemConfiguration : IEntityTypeConfiguration<SessionLoadItem>
{
    public void Configure(EntityTypeBuilder<SessionLoadItem> builder)
    {
        builder.ToTable("SessionLoadItems");
        builder.HasKey(i => i.Id);

        // Un producto no puede aparecer dos veces en la misma carga: seria un
        // segundo saldo del mismo envase en el mismo camion.
        builder.HasIndex(i => new { i.DeliverySessionId, i.ProductId }).IsUnique();

        // La carga es parte de la sesion, no existe sin ella.
        builder.HasOne(i => i.DeliverySession)
               .WithMany(s => s.LoadItems)
               .HasForeignKey(i => i.DeliverySessionId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Product)
               .WithMany()
               .HasForeignKey(i => i.ProductId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
