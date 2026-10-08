using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversityParking.Domain.Parking;

namespace UniversityParking.Infrastructure.Persistence.Configurations;

public sealed class ParkingZoneConfiguration : IEntityTypeConfiguration<ParkingZone>
{
    public void Configure(EntityTypeBuilder<ParkingZone> builder)
    {
        ConfigurationDefaults.Entity(builder, "parking_zones");
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.VehicleType).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasOne<ParkingLot>().WithMany().HasForeignKey(x => x.ParkingLotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ParkingLotId, x.VehicleType }).IsUnique().HasDatabaseName("ux_parking_zones_lot_type");
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_parking_zones_type", "vehicle_type IN ('CAR', 'MOTORCYCLE', 'BICYCLE')");
            table.HasCheckConstraint("ck_parking_zones_status", "status IN ('ACTIVE', 'INACTIVE')");
        });
    }
}
