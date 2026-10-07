using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderFlow.Orders.Domain.Orders;

namespace OrderFlow.Orders.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(order => order.Id);
        builder.Property(order => order.Id).ValueGeneratedNever();

        builder.Property(order => order.IdempotencyKey).HasMaxLength(100).IsRequired();
        builder.Property(order => order.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(order => order.TotalAmount).HasPrecision(18, 2);
        builder.Property(order => order.FailureReason).HasMaxLength(500);
        builder.Property(order => order.RowVersion).IsRowVersion();

        builder.HasIndex(order => new { order.CustomerId, order.IdempotencyKey }).IsUnique();
        builder.HasIndex(order => new { order.CustomerId, order.CreatedAtUtc });

        builder.HasMany(order => order.Lines).WithOne().OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(order => order.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(order => order.DomainEvents);
    }
}
