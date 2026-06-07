using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartTravelPlaners.DAL.Entities;

namespace SmartTravelPlaners.DAL.Configurations
{
    public class TripConfiguration : IEntityTypeConfiguration<Trip>
    {
        public void Configure(EntityTypeBuilder<Trip> builder)
        {
            builder.HasKey(t => t.Id);

            builder.Property(t => t.UserId)
                .IsRequired();

            builder.Property(t => t.Title)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(t => t.Destination)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(t => t.OriginCity)
                .HasMaxLength(200);

            builder.Property(t => t.StartDate)
                .IsRequired();

            builder.Property(t => t.EndDate)
                .IsRequired();

            builder.Property(t => t.NumTravelers)
                .IsRequired();

            builder.Property(t => t.BudgetTotal)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            builder.Property(t => t.BudgetSpent)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            builder.Property(t => t.Status)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(t => t.CreatedAt)
                .IsRequired();

            // Relationship with UserProfile
            builder.HasOne(t => t.User)
                .WithMany(up => up.Trips)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
