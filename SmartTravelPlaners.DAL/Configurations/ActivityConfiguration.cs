using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartTravelPlaners.DAL.Entities;

namespace SmartTravelPlaners.DAL.Configurations
{
    public class ActivityConfiguration : IEntityTypeConfiguration<Activity>
    {
        public void Configure(EntityTypeBuilder<Activity> builder)
        {
            builder.HasKey(a => a.Id);

            builder.Property(a => a.TripDayId)
                .IsRequired();

            builder.Property(a => a.Name)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(a => a.Type)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(a => a.LocationName)
                .HasMaxLength(500);

            builder.Property(a => a.Lat);

            builder.Property(a => a.Lng);

            builder.Property(a => a.StartTime);

            builder.Property(a => a.EstimatedCost)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            builder.Property(a => a.BookingUrl)
                .HasMaxLength(2048);

            builder.Property(a => a.Status)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(a => a.Notes)
                .HasMaxLength(2000);

            builder.Property(a => a.PlaceId)
                .HasMaxLength(100);

            // Relationship with TripDay
            builder.HasOne(a => a.TripDay)
                .WithMany(td => td.Activities)
                .HasForeignKey(a => a.TripDayId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
