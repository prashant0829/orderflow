using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OrderFlow.Orders.Infrastructure.Sagas;

public class OrderStateMap : SagaClassMap<OrderState>
{
    protected override void Configure(EntityTypeBuilder<OrderState> entity, ModelBuilder model)
    {
        entity.ToTable("OrderSagas");
        entity.Property(saga => saga.CurrentState).HasMaxLength(64).IsRequired();
        entity.Property(saga => saga.TotalAmount).HasPrecision(18, 2);
        entity.Property(saga => saga.FailureReason).HasMaxLength(500);
        entity.Property(saga => saga.PaymentTransactionId).HasMaxLength(100);
        entity.Property(saga => saga.RowVersion).IsRowVersion();
        entity.HasIndex(saga => saga.CurrentState);
    }
}
