using OrderFlow.Payments.Domain.Payments;

namespace OrderFlow.Payments.Application.Payments.Dtos;

public record PaymentDto(
    Guid Id,
    Guid OrderId,
    Guid CustomerId,
    decimal Amount,
    string Status,
    string? TransactionId,
    string? FailureReason,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public static class PaymentMapping
{
    public static PaymentDto ToDto(this Payment payment) => new(
        payment.Id, payment.OrderId, payment.CustomerId, payment.Amount, payment.Status.ToString(),
        payment.TransactionId, payment.FailureReason, payment.CreatedAtUtc, payment.UpdatedAtUtc);
}
