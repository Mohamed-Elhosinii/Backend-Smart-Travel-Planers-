using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartTravelPlaners.DAL.Entities;

namespace SmartTravelPlaners.DAL.Configurations
{
    public class ExternalApiCacheConfiguration : IEntityTypeConfiguration<ExternalApiCache>
    {
        public void Configure(EntityTypeBuilder<ExternalApiCache> builder)
        {
            builder.HasKey(e => e.Id);

            builder.Property(e => e.CacheKey)
                .IsRequired()
                .HasMaxLength(255);

            builder.Property(e => e.Source)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(e => e.PayloadJson)
                .IsRequired()
                .HasColumnType("nvarchar(max)");

            builder.Property(e => e.CreatedAt)
                .IsRequired();

            builder.Property(e => e.ExpiresAt)
                .IsRequired();

            // Index for fast lookups
            builder.HasIndex(e => new { e.CacheKey, e.Source }).IsUnique();
        }
    }
}
