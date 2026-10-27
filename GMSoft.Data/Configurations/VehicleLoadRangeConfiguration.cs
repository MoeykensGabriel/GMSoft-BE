using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMSoft.Data.Configurations;

public class VehicleLoadRangeConfiguration : IEntityTypeConfiguration<VehicleLoadRange>
{
    public void Configure(EntityTypeBuilder<VehicleLoadRange> b)
    {
        b.ToTable("VehicleLoadRanges");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.VehicleId, x.ProductId }).IsUnique();
        b.HasOne<Vehicle>().WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}
