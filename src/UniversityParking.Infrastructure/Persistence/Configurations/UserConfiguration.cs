using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Users.ValueObjects;

namespace UniversityParking.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        ConfigurationDefaults.Entity(builder, "users");
        builder.Property(x => x.IdentificationNumber).HasConversion(x => x.Value, x => new IdentificationNumber(x)).HasMaxLength(50).IsRequired();
        builder.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.UniversityId).IsRequired();
        builder.HasIndex(x => x.UniversityId).HasDatabaseName("ix_users_university_id");
        builder.HasOne<UniversityParking.Domain.Universities.University>().WithMany()
            .HasForeignKey(x => x.UniversityId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Career).HasMaxLength(200);
        builder.Property(x => x.MemberType).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.CardCode).HasConversion(x => x.Value, x => new CardCode(x)).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.IdentificationNumber).IsUnique().HasDatabaseName("ux_users_identification_number");
        builder.HasIndex(x => x.CardCode).IsUnique().HasDatabaseName("ux_users_card_code");
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_users_member_type", "member_type IN ('STUDENT', 'TEACHER', 'STAFF')");
            table.HasCheckConstraint("ck_users_status", "status IN ('ACTIVE', 'INACTIVE')");
            table.HasCheckConstraint("ck_users_student_career", "member_type <> 'STUDENT' OR (career IS NOT NULL AND btrim(career) <> '')");
        });
    }
}
