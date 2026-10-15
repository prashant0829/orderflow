using OrderFlow.Payments.Domain.Payments;

namespace OrderFlow.Payments.Application.Payments.Dtos;

public record PaymentDto(
    Guid Id,
    Guid OrderId,
    Guid CustomerId,
    decimal Amount,
    string Status,
    string? TransactionId,
    string? ProviderPaymentId,
    string? FailureReason,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

/// <summary>What the order page needs to take the customer's payment. The secret is only filled in while the customer still has to pay.</summary>
public record CheckoutDto(string Status, decimal Amount, string? Provider, string? PublishableKey, string? ClientSecret, string? Currency);

public static class PaymentMapping
{
    public static PaymentDto ToDto(this Payment payment) => new(
        payment.Id, payment.OrderId, payment.CustomerId, payment.Amount, payment.Status.ToString(),
        payment.TransactionId, payment.ProviderPaymentId, payment.FailureReason, payment.CreatedAtUtc, payment.UpdatedAtUtc);
}
