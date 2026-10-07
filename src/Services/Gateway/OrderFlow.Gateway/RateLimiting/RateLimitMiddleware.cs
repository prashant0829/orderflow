using System.Globalization;
using Microsoft.Extensions.Options;
using OrderFlow.Security;

namespace OrderFlow.Gateway.RateLimiting;

public class RateLimitMiddleware(RequestDelegate next, IRateLimiter limiter, IOptions<RateLimitOptions> options, ILogger<RateLimitMiddleware> logger)
{
    private static readonly string[] AuthPaths = ["/api/v1/auth/login", "/api/v1/auth/register", "/api/v1/auth/refresh"];

    public async Task InvokeAsync(HttpContext context)
    {
        var settings = options.Value;
        if (!settings.Enabled || HttpMethods.IsOptions(context.Request.Method))
        {
            await next(context);
            return;
        }

        var (policyName, policy, subject) = Classify(context, settings);
        var decision = await limiter.CheckAsync($"ratelimit:{policyName}:{subject}", policy, context.RequestAborted);

        GatewayTelemetry.RecordRateLimit(policyName, decision.Allowed);

        context.Response.Headers["X-RateLimit-Limit"] = decision.Limit.ToString(CultureInfo.InvariantCulture);
        context.Response.Headers["X-RateLimit-Remaining"] = decision.Remaining.ToString(CultureInfo.InvariantCulture);

        if (decision.Allowed)
        {
            await next(context);
            return;
        }

        var retryAfterSeconds = Math.Max((int)Math.Ceiling(decision.RetryAfter.TotalSeconds), 1);
        logger.LogWarning("Rate limit hit: policy {Policy}, subject {Subject}, path {Path}", policyName, subject, context.Request.Path);

        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
        await context.Response.WriteAsJsonAsync(new
        {
            status = StatusCodes.Status429TooManyRequests,
            title = "Too many requests",
            detail = $"Slow down. Try again in {retryAfterSeconds} seconds."
        }, context.RequestAborted);
    }

    private static (string Name, RateLimitPolicy Policy, string Subject) Classify(HttpContext context, RateLimitOptions settings)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (AuthPaths.Any(path => context.Request.Path.StartsWithSegments(path, StringComparison.OrdinalIgnoreCase)))
        {
            return (RateLimitOptions.AuthPolicy, settings.Auth, ip);
        }

        return context.User.Identity?.IsAuthenticated == true
            ? (RateLimitOptions.AuthenticatedPolicy, settings.Authenticated, context.User.GetUserId().ToString())
            : (RateLimitOptions.AnonymousPolicy, settings.Anonymous, ip);
    }
}
