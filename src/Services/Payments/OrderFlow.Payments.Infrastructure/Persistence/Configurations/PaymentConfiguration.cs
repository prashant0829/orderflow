using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderFlow.Payments.Domain.Payments;

namespace OrderFlow.Payments.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");
        builder.HasKey(payment => payment.Id);
        builder.Property(payment => payment.Id).ValueGeneratedNever();
        builder.Property(payment => payment.Amount).HasPrecision(18, 2);
        builder.Property(payment => payment.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(payment => payment.TransactionId).HasMaxLength(100);
        builder.Property(payment => payment.ProviderPaymentId).HasMaxLength(100);
        builder.Property(payment => payment.FailureReason).HasMaxLength(500);
        builder.Property(payment => payment.RowVersion).IsRowVersion();

        // One payment per order: the safety net behind the "never charge twice" rule.
        builder.HasIndex(payment => payment.OrderId).IsUnique();
        builder.HasIndex(payment => payment.CreatedAtUtc);
        builder.HasIndex(payment => payment.ProviderPaymentId);
    }
}
