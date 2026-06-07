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

            builder.Property(cs => cs.TripId)
                .IsRequired();

            builder.Property(cs => cs.UserId)
                .IsRequired();

            builder.Property(cs => cs.CreatedAt)
                .IsRequired();

            builder.Property(cs => cs.UpdatedAt)
                .IsRequired();

            // 1-to-1 Relationship with Trip
            builder.HasOne(cs => cs.Trip)
                .WithOne(t => t.ChatSession)
                .HasForeignKey<ChatSession>(cs => cs.TripId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relationship with ApplicationUser
            builder.HasOne(cs => cs.User)
                .WithMany(u => u.ChatSessions)
                .HasForeignKey(cs => cs.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
