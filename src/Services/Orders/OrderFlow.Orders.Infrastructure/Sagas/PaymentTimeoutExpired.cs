namespace OrderFlow.Orders.Infrastructure.Sagas;

/// <summary>Scheduled by the saga when payment starts. If it arrives while still awaiting payment, the order is compensated.</summary>
public record PaymentTimeoutExpired(Guid OrderId);
