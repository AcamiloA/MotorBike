using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversityParking.Domain.Incidents;
using UniversityParking.Domain.Parking;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Infrastructure.Persistence.Configurations;

public sealed class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    public void Configure(EntityTypeBuilder<Incident> builder)
    {
        ConfigurationDefaults.Entity(builder, "incidents");
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Description).HasColumnType("text").IsRequired();
        builder.Property(x => x.Resolution).HasColumnType("text");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Vehicle>().WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ParkingMovement>().WithMany().HasForeignKey(x => x.ParkingMovementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ParkingLot>().WithMany().HasForeignKey(x => x.ParkingLotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ReportedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ResolvedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.Status).HasDatabaseName("ix_incidents_status");
        builder.HasIndex(x => x.Type).HasDatabaseName("ix_incidents_type");
        builder.HasIndex(x => x.OccurredAt).HasDatabaseName("ix_incidents_occurred_at");
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_incidents_type", "type IN ('DAMAGE', 'ACCIDENT', 'SECURITY', 'DOCUMENT', 'BEHAVIOR', 'OTHER')");
            table.HasCheckConstraint("ck_incidents_status", "status IN ('OPEN', 'RESOLVED', 'CANCELLED')");
            table.HasCheckConstraint("ck_incidents_resolution", "(status = 'RESOLVED' AND resolution IS NOT NULL AND resolved_by IS NOT NULL AND resolved_at IS NOT NULL) OR (status = 'OPEN' AND resolved_by IS NULL AND resolved_at IS NULL) OR status = 'CANCELLED'");
        });
    }
}
