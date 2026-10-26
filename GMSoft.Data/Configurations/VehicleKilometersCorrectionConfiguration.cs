using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMSoft.Data.Configurations;

public class VehicleKilometersCorrectionConfiguration : IEntityTypeConfiguration<VehicleKilometersCorrection>
{
    public void Configure(EntityTypeBuilder<VehicleKilometersCorrection> builder)
    {
        builder.ToTable("VehicleKilometersCorrections");
        builder.HasKey(c => c.Id);
        builder.HasIndex(c => new { c.VehicleId, c.CorrectedAt });
        builder.Property(c => c.Reason).HasMaxLength(300).IsRequired();
        builder.HasOne(c => c.Vehicle).WithMany().HasForeignKey(c => c.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
