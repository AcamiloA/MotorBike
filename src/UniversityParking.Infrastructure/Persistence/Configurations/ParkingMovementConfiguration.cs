using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversityParking.Domain.Parking;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Infrastructure.Persistence.Configurations;

public sealed class ParkingMovementConfiguration : IEntityTypeConfiguration<ParkingMovement>
{
    public void Configure(EntityTypeBuilder<ParkingMovement> builder)
    {
        ConfigurationDefaults.Entity(builder, "parking_movements");
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Vehicle>().WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ParkingLot>().WithMany().HasForeignKey(x => x.ParkingLotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ParkingZone>().WithMany().HasForeignKey(x => x.ParkingZoneId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CheckInGuardId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CheckOutGuardId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.VehicleId).IsUnique().HasFilter("status = 'OPEN'").HasDatabaseName("ux_parking_movements_open_vehicle");
        builder.HasIndex(x => x.UserId).IsUnique().HasFilter("status = 'OPEN'").HasDatabaseName("ux_parking_movements_open_user");
        builder.HasIndex(x => new { x.VehicleId, x.CheckInAt }).HasDatabaseName("ix_parking_movements_vehicle_check_in");
        builder.HasIndex(x => new { x.UserId, x.CheckInAt }).HasDatabaseName("ix_parking_movements_user_check_in");
        builder.HasIndex(x => x.CheckInAt).HasDatabaseName("ix_parking_movements_check_in");
        builder.HasIndex(x => x.Status).HasDatabaseName("ix_parking_movements_status");
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_parking_movements_status", "status IN ('OPEN', 'CLOSED')");
            table.HasCheckConstraint("ck_parking_movements_state", "(status = 'OPEN' AND check_out_at IS NULL AND check_out_guard_id IS NULL) OR (status = 'CLOSED' AND check_out_at IS NOT NULL AND check_out_guard_id IS NOT NULL AND check_out_at >= check_in_at)");
        });
    }
}
