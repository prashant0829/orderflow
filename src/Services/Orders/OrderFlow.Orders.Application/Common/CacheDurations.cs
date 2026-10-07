namespace OrderFlow.Orders.Application.Common;

public static class CacheDurations
{
    public static readonly TimeSpan Order = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan Idempotency = TimeSpan.FromHours(24);
}
