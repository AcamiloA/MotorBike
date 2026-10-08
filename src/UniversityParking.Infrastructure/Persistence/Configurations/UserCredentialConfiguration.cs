using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversityParking.Domain.Users;

namespace UniversityParking.Infrastructure.Persistence.Configurations;

public sealed class UserCredentialConfiguration : IEntityTypeConfiguration<UserCredential>
{
    public void Configure(EntityTypeBuilder<UserCredential> builder)
    {
        builder.ToTable("user_credentials");
        builder.HasKey(x => x.UserId);
        builder.Property(x => x.UserId).ValueGeneratedNever();
        builder.Property(x => x.PasswordHash).HasColumnType("text").IsRequired();
        builder.HasOne<User>().WithOne().HasForeignKey<UserCredential>(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
