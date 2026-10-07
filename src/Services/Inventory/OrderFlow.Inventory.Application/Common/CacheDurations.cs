namespace OrderFlow.Inventory.Application.Common;

/// <summary>Short on purpose: reads may lag slightly, but reservations always check the database.</summary>
public static class CacheDurations
{
    public static readonly TimeSpan Stock = TimeSpan.FromSeconds(30);
}
