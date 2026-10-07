namespace OrderFlow.SharedKernel;

public class NotFoundException : Exception
{
    public NotFoundException(string resource, object key) : base($"{resource} '{key}' was not found.")
    {
    }

    public NotFoundException()
    {
    }

    public NotFoundException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
