namespace OrderFlow.Messaging.DeadLetters;

public interface IDeadLetterService
{
    Task<IReadOnlyList<DeadLetterQueueInfo>> ListAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<DeadLetterMessage>> PeekAsync(string errorQueue, int count, CancellationToken cancellationToken);

    Task<ReplayResult> ReplayAsync(string errorQueue, CancellationToken cancellationToken);
}
