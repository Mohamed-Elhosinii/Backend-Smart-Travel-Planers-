
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartTravelPlaners.DAL.Entities;

namespace SmartTravelPlaners.DAL.Configurations
{
    public class TripPreferenceConfiguration : IEntityTypeConfiguration<TripPreference>
    {
        public void Configure(EntityTypeBuilder<TripPreference> builder)
        {
            builder.HasKey(tp => tp.Id);

            builder.Property(tp => tp.TripId)
                .IsRequired();

            builder.Property(tp => tp.Category)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(tp => tp.Value)
                .IsRequired()
                .HasMaxLength(200);

            // Relationship with Trip
            builder.HasOne(tp => tp.Trip)
                .WithMany(t => t.Preferences)
                .HasForeignKey(tp => tp.TripId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}