namespace OrderFlow.Notifications;

public class NotificationOptions
{
    public const string SectionName = "Notifications";

    /// <summary>Orders from this customer always fail. Used to demonstrate retries and the dead-letter queue.</summary>
    public Guid? PoisonCustomerId { get; set; }
}
