using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Notifications.Persistence;
using OrderFlow.Security;

namespace OrderFlow.Notifications.Controllers;

public record NotificationDto(Guid Id, Guid OrderId, string Type, string Channel, string Recipient, string Subject, string Body, DateTime CreatedAtUtc);

public record TimelineEntryDto(string State, string Message, DateTime OccurredAtUtc);

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
public class NotificationsController(NotificationsDbContext dbContext) : ControllerBase
{
    private const int MaxPageSize = 100;

    [HttpGet("notifications")]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> List(
        [FromQuery] Guid? customerId, [FromQuery] int count = 50, CancellationToken cancellationToken = default)
    {
        var target = User.IsAdmin() && customerId is { } requested ? requested : User.GetUserId();

        return Ok(await dbContext.SentNotifications.AsNoTracking()
            .Where(notification => notification.CustomerId == target)
            .OrderByDescending(notification => notification.CreatedAtUtc)
            .Take(Math.Clamp(count, 1, MaxPageSize))
            .Select(notification => new NotificationDto(notification.Id, notification.OrderId, notification.Type, notification.Channel,
                notification.Recipient, notification.Subject, notification.Body, notification.CreatedAtUtc))
            .ToListAsync(cancellationToken));
    }

    [HttpGet("timeline/{orderId:guid}")]
    public async Task<ActionResult<IReadOnlyList<TimelineEntryDto>>> Timeline(Guid orderId, CancellationToken cancellationToken)
    {
        var entries = await dbContext.TimelineEntries.AsNoTracking()
            .Where(entry => entry.OrderId == orderId)
            .OrderBy(entry => entry.OccurredAtUtc)
            .ToListAsync(cancellationToken);

        // Someone else's order looks the same as an unknown one.
        if (entries.Count > 0 && !User.CanAccessCustomer(entries[0].CustomerId))
        {
            return NotFound();
        }

        return Ok(entries.Select(entry => new TimelineEntryDto(entry.State, entry.Message, entry.OccurredAtUtc)).ToList());
    }
}
