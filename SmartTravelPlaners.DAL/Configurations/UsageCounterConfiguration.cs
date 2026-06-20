using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartTravelPlaners.DAL.Entities;

namespace SmartTravelPlaners.DAL.Configurations
{
    public class UsageCounterConfiguration : IEntityTypeConfiguration<UsageCounter>
    {
        public void Configure(EntityTypeBuilder<UsageCounter> builder)
        {
            builder.HasKey(uc => uc.Id);

            builder.Property(uc => uc.PeriodMonth)
                .IsRequired()
                .HasMaxLength(7); // "yyyy-MM"

            builder.Property(uc => uc.TripsUsed)
                .IsRequired()
                .HasDefaultValue(0);

            builder.Property(uc => uc.MessagesUsed)
                .IsRequired()
                .HasDefaultValue(0);

            // Unique index: one counter row per user per month
            builder.HasIndex(uc => new { uc.UserProfileId, uc.PeriodMonth })
                .IsUnique();

            // FK → UserProfile
            builder.HasOne(uc => uc.UserProfile)
                .WithMany(up => up.UsageCounters)
                .HasForeignKey(uc => uc.UserProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
