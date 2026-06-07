using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartTravelPlaners.DAL.Entities;

namespace SmartTravelPlaners.DAL.Configurations
{
    public class FlightConfiguration : IEntityTypeConfiguration<Flight>
    {
        public void Configure(EntityTypeBuilder<Flight> builder)
        {
            builder.HasKey(f => f.Id);

            builder.Property(f => f.TripId)
                .IsRequired();

            builder.Property(f => f.Airline)
                .HasMaxLength(100);

            builder.Property(f => f.FlightNumber)
                .HasMaxLength(50);

            builder.Property(f => f.Origin)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(f => f.Destination)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(f => f.DepartureTime)
                .IsRequired();

            builder.Property(f => f.ArrivalTime)
                .IsRequired();

            builder.Property(f => f.Price)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            builder.Property(f => f.BookingUrl)
                .HasMaxLength(2048);

            builder.Property(f => f.CabinClass)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(f => f.Status)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            // Relationship with Trip
            builder.HasOne(f => f.Trip)
                .WithMany(t => t.Flights)
                .HasForeignKey(f => f.TripId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
