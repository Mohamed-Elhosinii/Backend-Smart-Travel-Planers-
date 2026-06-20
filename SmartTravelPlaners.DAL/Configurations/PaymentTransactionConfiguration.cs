using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartTravelPlaners.DAL.Entities;

namespace SmartTravelPlaners.DAL.Configurations
{
    public class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
    {
        public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
        {
            builder.HasKey(pt => pt.Id);

            builder.Property(pt => pt.PaymobOrderId)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(pt => pt.PaymobTransactionId)
                .HasMaxLength(200);

            builder.Property(pt => pt.Amount)
                .IsRequired()
                .HasColumnType("decimal(18,2)");

            builder.Property(pt => pt.Status)
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(pt => pt.CreatedAt)
                .IsRequired();

            // Index for webhook lookup by Paymob order ID
            builder.HasIndex(pt => pt.PaymobOrderId);

            // FK → Subscription
            builder.HasOne(pt => pt.Subscription)
                .WithMany(s => s.PaymentTransactions)
                .HasForeignKey(pt => pt.SubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
