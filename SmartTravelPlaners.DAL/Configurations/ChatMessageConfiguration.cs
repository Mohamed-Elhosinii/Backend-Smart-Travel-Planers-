
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartTravelPlaners.DAL.Entities;

namespace SmartTravelPlaners.DAL.Configurations
{
    public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
    {
        public void Configure(EntityTypeBuilder<ChatMessage> builder)
        {
            builder.HasKey(cm => cm.Id);

            builder.Property(cm => cm.SessionId)
                .IsRequired();

            builder.Property(cm => cm.Role)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(cm => cm.Content)
                .IsRequired();

            builder.Property(cm => cm.ToolCallsJson);

            builder.Property(cm => cm.CreatedAt)
                .IsRequired();

            // Relationship with ChatSession
            builder.HasOne(cm => cm.Session)
                .WithMany(cs => cs.Messages)
                .HasForeignKey(cm => cm.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}