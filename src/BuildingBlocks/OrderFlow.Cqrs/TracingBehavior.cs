using System.Diagnostics;
using MediatR;

namespace OrderFlow.Cqrs;

/// <summary>One span per command or query, so a trace shows which handler ran and how long it took.</summary>
public class TracingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public const string ActivitySourceName = "OrderFlow.Cqrs";
    private static readonly ActivitySource Source = new(ActivitySourceName);

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        using var activity = Source.StartActivity($"Handle {typeof(TRequest).Name}");
        try
        {
            return await next();
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.AddTag("exception.type", exception.GetType().Name);
            throw;
        }
    }
}
