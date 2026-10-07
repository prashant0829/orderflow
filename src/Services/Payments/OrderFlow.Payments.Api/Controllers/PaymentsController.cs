using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderFlow.Payments.Application.Payments.Dtos;
using OrderFlow.Payments.Application.Payments.Queries;
using OrderFlow.Security;

namespace OrderFlow.Payments.Api.Controllers;

[ApiController]
[Authorize(Policy = PolicyNames.AdminOnly)]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/payments")]
public class PaymentsController(ISender sender) : ControllerBase
{
    [HttpGet("order/{orderId:guid}")]
    public async Task<ActionResult<PaymentDto>> GetByOrder(Guid orderId, CancellationToken cancellationToken) =>
        await sender.Send(new GetPaymentByOrderQuery(orderId), cancellationToken);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PaymentDto>>> ListRecent([FromQuery] int count = 50, CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new ListRecentPaymentsQuery(count), cancellationToken));
}
