using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartTravelPlaners.DAL.Entities;

namespace SmartTravelPlaners.DAL.Configurations
{
    public class WeatherDayConfiguration : IEntityTypeConfiguration<WeatherDay>
    {
        public void Configure(EntityTypeBuilder<WeatherDay> builder)
        {
            builder.HasKey(w => w.Id);

            builder.Property(w => w.TripId)
                .IsRequired();

            builder.Property(w => w.Date)
                .IsRequired();

            builder.Property(w => w.TempMax)
                .IsRequired();

            builder.Property(w => w.TempMin)
                .IsRequired();

            builder.Property(w => w.Humidity)
                .IsRequired();

            builder.Property(w => w.PrecipProb)
                .IsRequired();

            builder.Property(w => w.Conditions)
                .HasMaxLength(200);

            builder.Property(w => w.IconUrl)
                .HasMaxLength(2048);

            // Relationship with Trip
            builder.HasOne(w => w.Trip)
                .WithMany(t => t.WeatherDays)
                .HasForeignKey(w => w.TripId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
