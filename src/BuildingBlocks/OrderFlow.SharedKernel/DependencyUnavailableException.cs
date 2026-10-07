namespace OrderFlow.SharedKernel;

/// <summary>A service we depend on is down or too slow. Maps to HTTP 503.</summary>
public class DependencyUnavailableException : Exception
{
    public DependencyUnavailableException(string message) : base(message)
    {
    }

    public DependencyUnavailableException()
    {
    }

    public DependencyUnavailableException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
