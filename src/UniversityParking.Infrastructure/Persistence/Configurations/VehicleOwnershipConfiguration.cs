using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Infrastructure.Persistence.Configurations;

public sealed class VehicleOwnershipConfiguration : IEntityTypeConfiguration<VehicleOwnership>
{
    public void Configure(EntityTypeBuilder<VehicleOwnership> builder)
    {
        ConfigurationDefaults.Entity(builder, "vehicle_ownerships");
        builder.Property(x => x.TransferReason).HasMaxLength(500);
        builder.HasOne<Vehicle>().WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.VehicleId).IsUnique().HasFilter("end_at IS NULL").HasDatabaseName("ux_vehicle_ownerships_current_vehicle");
        builder.HasIndex(x => new { x.UserId, x.EndAt }).HasDatabaseName("ix_vehicle_ownerships_user_end_at");
        builder.ToTable(table => table.HasCheckConstraint("ck_vehicle_ownerships_dates", "end_at IS NULL OR end_at >= start_at"));
    }
}
