namespace OrderFlow.Gateway.RateLimiting;

public class RateLimitPolicy
{
    public int Limit { get; set; }
    public int WindowSeconds { get; set; }
}

public class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    public const string AuthPolicy = "auth";
    public const string AuthenticatedPolicy = "authenticated";
    public const string AnonymousPolicy = "anonymous";

    public bool Enabled { get; set; } = true;

    /// <summary>Login, register and refresh: strict, per client IP, to slow down password guessing.</summary>
    public RateLimitPolicy Auth { get; set; } = new() { Limit = 10, WindowSeconds = 60 };

    /// <summary>Signed-in callers: per user id.</summary>
    public RateLimitPolicy Authenticated { get; set; } = new() { Limit = 120, WindowSeconds = 60 };

    /// <summary>Everything else without a token: per client IP.</summary>
    public RateLimitPolicy Anonymous { get; set; } = new() { Limit = 60, WindowSeconds = 60 };
}
