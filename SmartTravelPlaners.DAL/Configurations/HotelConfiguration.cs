using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartTravelPlaners.DAL.Entities;

namespace SmartTravelPlaners.DAL.Configurations
{
    public class HotelConfiguration : IEntityTypeConfiguration<Hotel>
    {
        public void Configure(EntityTypeBuilder<Hotel> builder)
        {
            builder.HasKey(h => h.Id);

            builder.Property(h => h.TripId)
                .IsRequired();

            builder.Property(h => h.Name)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(h => h.Address)
                .HasMaxLength(500);

            builder.Property(h => h.Lat);

            builder.Property(h => h.Lng);

            builder.Property(h => h.CheckIn)
                .IsRequired();

            builder.Property(h => h.CheckOut)
                .IsRequired();

            builder.Property(h => h.PricePerNight)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            builder.Property(h => h.BookingUrl)
                .HasMaxLength(2048);

            builder.Property(h => h.Stars)
                .IsRequired();

            builder.Property(h => h.Status)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            // Relationship with Trip
            builder.HasOne(h => h.Trip)
                .WithMany(t => t.Hotels)
                .HasForeignKey(h => h.TripId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
