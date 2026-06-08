
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartTravelPlaners.DAL.Entities;

namespace SmartTravelPlaners.DAL.Configurations
{
    public class PlaceCacheConfiguration : IEntityTypeConfiguration<PlaceCache>
    {
        public void Configure(EntityTypeBuilder<PlaceCache> builder)
        {
            builder.HasKey(pc => pc.Id);

            builder.Property(pc => pc.PlaceId)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(pc => pc.Name)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(pc => pc.Type)
                .HasMaxLength(100);

            builder.Property(pc => pc.Lat)
                .IsRequired();

            builder.Property(pc => pc.Lng)
                .IsRequired();

            builder.Property(pc => pc.City)
                .HasMaxLength(100);

            builder.Property(pc => pc.Country)
                .HasMaxLength(100);

            builder.Property(pc => pc.Rating);

            builder.Property(pc => pc.Description)
                .HasMaxLength(2000);

            builder.Property(pc => pc.PhotosJson);

            builder.Property(pc => pc.OpeningHoursJson);

            builder.Property(pc => pc.CachedAt)
                .IsRequired();
        }
    }
}