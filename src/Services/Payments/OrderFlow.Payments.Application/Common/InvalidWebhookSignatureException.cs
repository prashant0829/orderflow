namespace OrderFlow.Payments.Application.Common;

public class InvalidWebhookSignatureException : Exception
{
    public InvalidWebhookSignatureException(string message) : base(message)
    {
    }

    public InvalidWebhookSignatureException()
    {
    }

    public InvalidWebhookSignatureException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
