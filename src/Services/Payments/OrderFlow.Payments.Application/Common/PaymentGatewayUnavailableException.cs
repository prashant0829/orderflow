namespace OrderFlow.Payments.Application.Common;

/// <summary>Temporary gateway problem (timeout, 5xx, circuit open). Different from a card being declined.</summary>
public class PaymentGatewayUnavailableException : Exception
{
    public PaymentGatewayUnavailableException(string message) : base(message)
    {
    }

    public PaymentGatewayUnavailableException()
    {
    }

    public PaymentGatewayUnavailableException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
