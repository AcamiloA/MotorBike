using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversityParking.Domain.Parking;

namespace UniversityParking.Infrastructure.Persistence.Configurations;

public sealed class ParkingLotConfiguration : IEntityTypeConfiguration<ParkingLot>
{
    public void Configure(EntityTypeBuilder<ParkingLot> builder)
    {
        ConfigurationDefaults.Entity(builder, "parking_lots");
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Campus).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(x => new { x.Name, x.Campus }).IsUnique().HasDatabaseName("ux_parking_lots_name_campus");
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_parking_lots_status", "status IN ('ACTIVE', 'INACTIVE')");
            table.HasCheckConstraint("ck_parking_lots_schedule", "opening_time < closing_time");
        });
    }
}
