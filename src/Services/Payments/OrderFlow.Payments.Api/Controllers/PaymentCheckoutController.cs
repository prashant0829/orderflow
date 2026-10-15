using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderFlow.Payments.Application.Payments.Commands;
using OrderFlow.Payments.Application.Payments.Dtos;
using OrderFlow.Payments.Application.Payments.Queries;
using OrderFlow.Security;

namespace OrderFlow.Payments.Api.Controllers;

/// <summary>What a customer can do with the payment of their own order: get the card form data, and ask for a fresh status after paying.</summary>
[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/payments/order/{orderId:guid}")]
public class PaymentCheckoutController(ISender sender) : ControllerBase
{
    /// <summary>404 until the saga has asked Payments to charge (a moment after the order is placed): the page simply asks again.</summary>
    [HttpGet("checkout")]
    public async Task<ActionResult<CheckoutDto>> Checkout(Guid orderId, CancellationToken cancellationToken)
    {
        var payment = await sender.Send(new GetPaymentByOrderQuery(orderId), cancellationToken);
        return CanAccess(payment) ? await sender.Send(new GetCheckoutQuery(orderId), cancellationToken) : NotFound();
    }

    /// <summary>Asks the provider where the payment stands. The order page calls this right after the customer pays, so it does not depend on the webhook.</summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<CheckoutDto>> Refresh(Guid orderId, CancellationToken cancellationToken)
    {
        var payment = await sender.Send(new GetPaymentByOrderQuery(orderId), cancellationToken);
        if (!CanAccess(payment))
        {
            return NotFound();
        }

        await sender.Send(new RefreshPaymentCommand(orderId), cancellationToken);
        return await sender.Send(new GetCheckoutQuery(orderId), cancellationToken);
    }

    // Someone else's payment looks exactly like a missing one.
    private bool CanAccess(PaymentDto payment) => User.CanAccessCustomer(payment.CustomerId);
}
