using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartTravelPlaners.DAL.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartTravelPlaners.DAL.Entities;

namespace SmartTravelPlaners.DAL.Configurations
{
    public class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
    {
        public void Configure(EntityTypeBuilder<UserProfile> builder)
        {
            builder.HasKey(up => up.Id);

            builder.Property(up => up.AspNetUserId)
                .IsRequired();

            builder.Property(up => up.PreferredCurrency)
                .HasMaxLength(10);

            builder.Property(up => up.AvatarUrl)
                .HasMaxLength(2048);

            builder.Property(up => up.CreatedAt)
                .IsRequired();

            // 1-to-1 Relationship with ApplicationUser (represented by AspNetUsers in standard Identity)
            builder.HasOne(up => up.AspNetUser)
                .WithOne(u => u.Profile)
                .HasForeignKey<UserProfile>(up => up.AspNetUserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
