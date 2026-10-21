using GMSoft.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMSoft.Data.Configurations;

public class VehicleScheduleDayConfiguration : IEntityTypeConfiguration<VehicleScheduleDay>
{
    public void Configure(EntityTypeBuilder<VehicleScheduleDay> b)
    {
        b.ToTable("VehicleScheduleDays", t => t.HasCheckConstraint("CK_VehicleScheduleDays_Day", "\"Day\" BETWEEN 1 AND 7"));
        // Una fila por zona del día, en orden. La posición entra en la clave: las semanas
        // guardadas antes de las varias zonas quedan como posición 0 sin tocarlas.
        b.HasKey(x => new { x.VehicleId, x.Day, x.Position });
        b.ToTable(t => t.HasCheckConstraint("CK_VehicleScheduleDays_Position", "\"Position\" >= 0"));
        b.HasOne<Vehicle>().WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Zone).WithMany().HasForeignKey(x => x.ZoneId).OnDelete(DeleteBehavior.Restrict);
    }
}
