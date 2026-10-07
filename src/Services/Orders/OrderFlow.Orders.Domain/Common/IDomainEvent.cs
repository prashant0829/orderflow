namespace OrderFlow.Orders.Domain.Common;

public interface IDomainEvent
{
    DateTime OccurredAtUtc { get; }
}
