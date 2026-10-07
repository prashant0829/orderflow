namespace OrderFlow.Messaging.DeadLetters;

public record DeadLetterQueueInfo(string Queue, string OriginalQueue, int MessageCount);

public record DeadLetterMessage(
    string? MessageId,
    string Payload,
    string? ExceptionType,
    string? ExceptionMessage,
    string? FailedAtUtc);

public record ReplayResult(string Queue, int Replayed);
