using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace OrderFlow.Observability;

/// <summary>Reads (or creates) X-Correlation-Id, adds it to every log line of the request and echoes it in the response.</summary>
public class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";
    private const int MaxLength = 64;

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].ToString();
        var correlationId = IsAcceptable(incoming) ? incoming : Guid.NewGuid().ToString("N");

        context.Request.Headers[HeaderName] = correlationId;

        // Set at the last moment and by assignment, so a proxied response that already carries the header does not end up with two values.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }

    // Only simple ids: anything else could be used to forge log lines.
    private static bool IsAcceptable(string value) =>
        value.Length is > 0 and <= MaxLength && value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_');
}

public static class CorrelationIdApplicationBuilderExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app) => app.UseMiddleware<CorrelationIdMiddleware>();
}
