namespace OrderFlow.SharedKernel;

/// <summary>A business rule was broken. Maps to HTTP 409.</summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }

    public DomainException()
    {
    }

    public DomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
