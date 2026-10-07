using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderFlow.Orders.Api.Contracts;
using OrderFlow.Orders.Application.Abstractions;
using OrderFlow.Orders.Application.Common;
using OrderFlow.Orders.Application.Orders.Commands;
using OrderFlow.Orders.Application.Orders.Dtos;
using OrderFlow.Orders.Application.Orders.Queries;
using OrderFlow.Security;

namespace OrderFlow.Orders.Api.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/orders")]
public class OrdersController(ISender sender) : ControllerBase
{
    public const string IdempotencyKeyHeader = "Idempotency-Key";

    [HttpPost]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(
        [FromBody] CreateOrderRequest request,
        [FromHeader(Name = IdempotencyKeyHeader)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Problem($"The '{IdempotencyKeyHeader}' header is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        // The customer is whoever the token says; the client cannot order on someone else's behalf.
        var result = await sender.Send(new CreateOrderCommand(User.GetUserId(), idempotencyKey, request.Lines), cancellationToken);

        return result.WasCreated
            ? CreatedAtAction(nameof(GetById), new { id = result.Order.Id, version = "1.0" }, result.Order)
            : Ok(result.Order);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrderDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var order = await sender.Send(new GetOrderQuery(id), cancellationToken);
        return CanAccess(order) ? order : NotFound();
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<OrderDto>>> List(
        [FromQuery] Guid? customerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PagingLimits.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        // Customers only ever see their own orders; admins may look at any customer.
        var target = User.IsAdmin() && customerId is { } requested ? requested : User.GetUserId();
        return await sender.Send(new ListOrdersQuery(target, page, pageSize), cancellationToken);
    }

    [HttpGet("{id:guid}/progress")]
    [ProducesResponseType(typeof(OrderProgress), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrderProgress>> GetProgress(Guid id, CancellationToken cancellationToken)
    {
        var order = await sender.Send(new GetOrderQuery(id), cancellationToken);
        return CanAccess(order) ? await sender.Send(new GetOrderProgressQuery(id), cancellationToken) : NotFound();
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<OrderDto>> Cancel(Guid id, [FromBody] CancelOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await sender.Send(new GetOrderQuery(id), cancellationToken);
        return CanAccess(order) ? await sender.Send(new CancelOrderCommand(id, request.Reason), cancellationToken) : NotFound();
    }

    // Someone else's order looks exactly like a missing one, so ids cannot be probed.
    private bool CanAccess(OrderDto order) => User.CanAccessCustomer(order.CustomerId);
}
