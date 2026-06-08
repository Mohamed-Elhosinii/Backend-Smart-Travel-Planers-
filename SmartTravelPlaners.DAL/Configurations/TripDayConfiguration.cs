
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartTravelPlaners.DAL.Entities;

namespace SmartTravelPlaners.DAL.Configurations
{
    public class TripDayConfiguration : IEntityTypeConfiguration<TripDay>
    {
        public void Configure(EntityTypeBuilder<TripDay> builder)
        {
            builder.HasKey(td => td.Id);

            builder.Property(td => td.TripId)
                .IsRequired();

            builder.Property(td => td.DayNumber)
                .IsRequired();

            builder.Property(td => td.Date)
                .IsRequired();

            builder.Property(td => td.BudgetAllocated)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            builder.Property(td => td.BudgetSpent)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            // Relationship with Trip
            builder.HasOne(td => td.Trip)
                .WithMany(t => t.Days)
                .HasForeignKey(td => td.TripId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}