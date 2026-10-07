using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderFlow.Orders.Application.Common;
using OrderFlow.Orders.Application.Orders.Dtos;
using OrderFlow.Orders.Application.Orders.Queries;
using OrderFlow.Security;

namespace OrderFlow.Orders.Api.Controllers;

[ApiController]
[Authorize(Policy = PolicyNames.AdminOnly)]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/orders")]
public class AdminOrdersController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<OrderDto>>> List(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PagingLimits.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        await sender.Send(new ListAllOrdersQuery(status, page, pageSize), cancellationToken);
}
