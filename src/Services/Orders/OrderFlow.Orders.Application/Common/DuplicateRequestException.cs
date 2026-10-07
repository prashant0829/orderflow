using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Application.Common;

public class DuplicateRequestException : ConflictException
{
    public DuplicateRequestException()
    {
    }

    public DuplicateRequestException(string message) : base(message)
    {
    }

    public DuplicateRequestException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
