using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversityParking.Domain.Vehicles;
using UniversityParking.Domain.Vehicles.ValueObjects;

namespace UniversityParking.Infrastructure.Persistence.Configurations;

public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        ConfigurationDefaults.Entity(builder, "vehicles");
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Plate).HasConversion(x => x!.Value, x => new VehiclePlate(x)).HasMaxLength(30);
        builder.Property(x => x.FrameNumber).HasConversion(x => x!.Value, x => new FrameNumber(x)).HasMaxLength(100);
        builder.Property(x => x.Brand).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Model).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Color).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.Plate).IsUnique().HasFilter("plate IS NOT NULL AND deleted_at IS NULL").HasDatabaseName("ux_vehicles_plate");
        builder.HasIndex(x => x.FrameNumber).IsUnique().HasFilter("frame_number IS NOT NULL AND deleted_at IS NULL").HasDatabaseName("ux_vehicles_frame_number");
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_vehicles_type", "type IN ('CAR', 'MOTORCYCLE', 'BICYCLE', 'SCOOTER')");
            table.HasCheckConstraint("ck_vehicles_status", "status IN ('ACTIVE', 'INACTIVE')");
            table.HasCheckConstraint("ck_vehicles_identifier", "(type IN ('CAR', 'MOTORCYCLE') AND plate IS NOT NULL AND frame_number IS NULL) OR (type IN ('BICYCLE', 'SCOOTER') AND plate IS NULL AND frame_number IS NOT NULL)");
        });
    }
}
