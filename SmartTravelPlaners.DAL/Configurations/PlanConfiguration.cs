using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartTravelPlaners.DAL.Entities;

namespace SmartTravelPlaners.DAL.Configurations
{
    public class PlanConfiguration : IEntityTypeConfiguration<Plan>
    {
        // Well-known Plan IDs for seeding — deterministic so migrations are repeatable.
        public static readonly Guid FreePlanId  = Guid.Parse("00000000-0000-0000-0000-000000000001");
        public static readonly Guid PlusPlanId  = Guid.Parse("00000000-0000-0000-0000-000000000002");
        public static readonly Guid ProPlanId   = Guid.Parse("00000000-0000-0000-0000-000000000003");

        public void Configure(EntityTypeBuilder<Plan> builder)
        {
            builder.HasKey(p => p.Id);

            builder.Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(p => p.PriceMonthly)
                .IsRequired()
                .HasColumnType("decimal(18,2)");

            builder.Property(p => p.MaxTripsPerMonth)
                .IsRequired(false);

            builder.Property(p => p.MaxMessagesPerMonth)
                .IsRequired(false);

            builder.Property(p => p.PaymobPlanId)
                .HasMaxLength(200);

            // Seed the 3 plans
            builder.HasData(
                new Plan
                {
                    Id = FreePlanId,
                    Name = "Free",
                    PriceMonthly = 0m,
                    MaxTripsPerMonth = 2,
                    MaxMessagesPerMonth = 30,
                    PaymobPlanId = null
                },
                new Plan
                {
                    Id = PlusPlanId,
                    Name = "Plus",
                    PriceMonthly = 4.99m,
                    MaxTripsPerMonth = 10,
                    MaxMessagesPerMonth = 300,
                    PaymobPlanId = null
                },
                new Plan
                {
                    Id = ProPlanId,
                    Name = "Pro",
                    PriceMonthly = 12.99m,
                    MaxTripsPerMonth = null,
                    MaxMessagesPerMonth = null,
                    PaymobPlanId = null
                }
            );
        }
    }
}
