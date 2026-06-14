using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartTravelPlaners.DAL.Entities;

namespace SmartTravelPlaners.DAL.Configurations
{
    public class ChatSessionConfiguration : IEntityTypeConfiguration<ChatSession>
    {
        public void Configure(EntityTypeBuilder<ChatSession> builder)
        {
            builder.HasKey(cs => cs.Id);

            // TripId is nullable - gets assigned after AI creates the trip
            builder.Property(cs => cs.TripId)
                .IsRequired(false);

            builder.Property(cs => cs.UserId)
                .IsRequired();

            builder.Property(cs => cs.Stage)
                .IsRequired();

            builder.Property(cs => cs.CreatedAt)
                .IsRequired();

            builder.Property(cs => cs.UpdatedAt)
                .IsRequired();

            // optional relationship with Trip
            builder.HasOne(cs => cs.Trip)
                .WithOne(t => t.ChatSession)
                .HasForeignKey<ChatSession>(cs => cs.TripId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            // relationship with ApplicationUser
            builder.HasOne(cs => cs.User)
                .WithMany(u => u.ChatSessions)
                .HasForeignKey(cs => cs.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}