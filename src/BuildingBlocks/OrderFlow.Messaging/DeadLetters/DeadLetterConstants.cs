namespace OrderFlow.Messaging.DeadLetters;

public static class DeadLetterConstants
{
    public const string ErrorQueueSuffix = "_error";
    public const string ManagementHttpClientName = "rabbitmq-management";
    public const string FaultHeaderPrefix = "MT-Fault-";
    public const string ReasonHeader = "MT-Reason";
    public const string FaultMessageHeader = "MT-Fault-Message";
    public const string FaultExceptionTypeHeader = "MT-Fault-ExceptionType";
    public const string FaultTimestampHeader = "MT-Fault-Timestamp";
    public const int MaxPeekCount = 50;
}
