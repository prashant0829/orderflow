namespace OrderFlow.Notifications.Sending;

/// <summary>Stands in for SMTP / SendGrid: writes the email to the log instead of sending it.</summary>
public class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken)
    {
        logger.LogInformation("EMAIL to {Recipient} | {Subject} | {Body}", recipient, subject, body);
        return Task.CompletedTask;
    }
}
