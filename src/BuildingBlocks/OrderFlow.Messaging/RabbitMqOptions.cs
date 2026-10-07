namespace OrderFlow.Messaging;

public class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string Host { get; set; } = "localhost";
    public ushort Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public int RetryCount { get; set; } = 5;
    public int RetryInitialIntervalMs { get; set; } = 500;
    public string ManagementUrl { get; set; } = "http://localhost:15672";
}
