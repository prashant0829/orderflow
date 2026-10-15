using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderFlow.Payments.Application.Abstractions;
using OrderFlow.Payments.Application.Common;
using OrderFlow.Payments.Application.Payments.Commands;

namespace OrderFlow.Payments.Api.Controllers;

/// <summary>
/// Stripe calls this when a payment succeeds, fails or is cancelled. It is open to the internet by design, so the only
/// thing that lets a request in is the signature Stripe puts on it (checked against the endpoint secret).
/// </summary>
[ApiController]
[AllowAnonymous]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/payments/webhooks/stripe")]
public class StripeWebhookController(IWebhookVerifier verifier, ISender sender, ILogger<StripeWebhookController> logger) : ControllerBase
{
    private const string SignatureHeader = "Stripe-Signature";

    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        if (!verifier.IsEnabled)
        {
            return NotFound();
        }

        // The signature covers the exact bytes Stripe sent, so read the raw body before anything parses it.
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);

        ProviderPaymentUpdate? update;
        try
        {
            update = verifier.Verify(body, Request.Headers[SignatureHeader]);
        }
        catch (InvalidWebhookSignatureException exception)
        {
            logger.LogWarning(exception, "Rejected a webhook with an invalid signature");
            return Problem("Invalid signature.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (update is not null)
        {
            await sender.Send(new ApplyProviderPaymentUpdateCommand(update.ProviderPaymentId, update.Outcome), cancellationToken);
        }

        // Always answer 200 for events we accept (even ones we ignore): any other answer makes Stripe retry for days.
        return Ok();
    }
}
