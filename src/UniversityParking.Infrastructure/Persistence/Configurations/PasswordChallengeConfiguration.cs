using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversityParking.Domain.Users;
namespace UniversityParking.Infrastructure.Persistence.Configurations;
public sealed class PasswordChallengeConfiguration : IEntityTypeConfiguration<PasswordChallenge>
{
    public void Configure(EntityTypeBuilder<PasswordChallenge> builder)
    {
        ConfigurationDefaults.Entity(builder,"password_challenges");
        builder.Property(x=>x.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(x=>x.Purpose).HasConversion<string>().HasMaxLength(30);
        builder.HasOne<User>().WithMany().HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x=>new { x.UserId, x.Purpose, x.CreatedAt });
    }
}
