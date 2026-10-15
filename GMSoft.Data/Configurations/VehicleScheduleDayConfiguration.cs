using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMSoft.Data.Configurations;

public class VehicleScheduleDayConfiguration : IEntityTypeConfiguration<VehicleScheduleDay>
{
    public void Configure(EntityTypeBuilder<VehicleScheduleDay> b)
    {
        b.ToTable("VehicleScheduleDays", t => t.HasCheckConstraint("CK_VehicleScheduleDays_Day", "\"Day\" BETWEEN 1 AND 7"));
        b.HasKey(x => new { x.VehicleId, x.Day });
        b.HasOne<Vehicle>().WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Zone).WithMany().HasForeignKey(x => x.ZoneId).OnDelete(DeleteBehavior.Restrict);
    }
}
