using System.Text.RegularExpressions;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderFlow.Messaging.DeadLetters;
using OrderFlow.Security;

namespace OrderFlow.Orders.Api.Controllers;

/// <summary>Operations on failed messages. Admin only.</summary>
[ApiController]
[Authorize(Policy = PolicyNames.AdminOnly)]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/dead-letters")]
public partial class DeadLettersController(IDeadLetterService deadLetterService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DeadLetterQueueInfo>>> List(CancellationToken cancellationToken) =>
        Ok(await deadLetterService.ListAsync(cancellationToken));

    [HttpGet("{queue}/messages")]
    public async Task<ActionResult<IReadOnlyList<DeadLetterMessage>>> Peek(
        string queue, [FromQuery] int count = 10, CancellationToken cancellationToken = default) =>
        IsErrorQueueName(queue)
            ? Ok(await deadLetterService.PeekAsync(queue, count, cancellationToken))
            : InvalidQueueName();

    [HttpPost("{queue}/replay")]
    public async Task<ActionResult<ReplayResult>> Replay(string queue, CancellationToken cancellationToken) =>
        IsErrorQueueName(queue)
            ? Ok(await deadLetterService.ReplayAsync(queue, cancellationToken))
            : InvalidQueueName();

    private ObjectResult InvalidQueueName() =>
        Problem($"Queue name must match '*{DeadLetterConstants.ErrorQueueSuffix}'.", statusCode: StatusCodes.Status400BadRequest);

    private static bool IsErrorQueueName(string queue) => ErrorQueueNamePattern().IsMatch(queue);

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]*_error$")]
    private static partial Regex ErrorQueueNamePattern();
}
