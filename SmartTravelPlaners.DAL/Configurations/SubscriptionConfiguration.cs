using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartTravelPlaners.DAL.Entities;

namespace SmartTravelPlaners.DAL.Configurations
{
    public class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
    {
        public void Configure(EntityTypeBuilder<Subscription> builder)
        {
            builder.HasKey(s => s.Id);

            builder.Property(s => s.Status)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(20);

            builder.Property(s => s.CurrentPeriodStart)
                .IsRequired();

            builder.Property(s => s.CurrentPeriodEnd)
                .IsRequired();

            builder.Property(s => s.PaymobSubscriptionId)
                .HasMaxLength(200);

            // FK → UserProfile
            builder.HasOne(s => s.UserProfile)
                .WithMany(up => up.Subscriptions)
                .HasForeignKey(s => s.UserProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            // FK → Plan
            builder.HasOne(s => s.Plan)
                .WithMany(p => p.Subscriptions)
                .HasForeignKey(s => s.PlanId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
