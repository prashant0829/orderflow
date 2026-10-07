namespace OrderFlow.SharedKernel;

/// <summary>The request conflicts with current state (duplicate, concurrent update). Maps to HTTP 409.</summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message)
    {
    }

    public ConflictException()
    {
    }

    public ConflictException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
