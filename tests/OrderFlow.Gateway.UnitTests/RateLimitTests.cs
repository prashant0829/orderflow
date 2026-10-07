using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using OrderFlow.Gateway.RateLimiting;
using StackExchange.Redis;

namespace OrderFlow.Gateway.UnitTests;

public class RateLimitTests
{
    private readonly IRateLimiter _limiter = Substitute.For<IRateLimiter>();
    private bool _nextCalled;

    private RateLimitMiddleware Middleware(RateLimitOptions? options = null) =>
        new(_ => { _nextCalled = true; return Task.CompletedTask; }, _limiter, Options.Create(options ?? new RateLimitOptions()), NullLogger<RateLimitMiddleware>.Instance);

    private static DefaultHttpContext Request(string path, string method = "GET", Guid? userId = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = method;
        context.Response.Body = new MemoryStream();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.1.2.3");
        if (userId is { } id)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", id.ToString())], "test"));
        }

        return context;
    }

    private void Decide(bool allowed, int remaining = 5) =>
        _limiter.CheckAsync(Arg.Any<string>(), Arg.Any<RateLimitPolicy>(), Arg.Any<CancellationToken>())
            .Returns(new RateLimitDecision(allowed, 10, remaining, TimeSpan.FromSeconds(42)));

    [Fact]
    public async Task LoginPath_UsesStrictPolicyPerIp()
    {
        Decide(true);

        await Middleware().InvokeAsync(Request("/api/v1/auth/login", "POST"));

        await _limiter.Received(1).CheckAsync("ratelimit:auth:10.1.2.3", Arg.Is<RateLimitPolicy>(policy => policy.Limit == 10), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SignedInCaller_IsLimitedPerUserNotPerIp()
    {
        Decide(true);
        var userId = Guid.NewGuid();

        await Middleware().InvokeAsync(Request("/api/v1/orders", userId: userId));

        await _limiter.Received(1).CheckAsync($"ratelimit:authenticated:{userId}", Arg.Any<RateLimitPolicy>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnonymousCaller_IsLimitedPerIp()
    {
        Decide(true);

        await Middleware().InvokeAsync(Request("/api/v1/stock"));

        await _limiter.Received(1).CheckAsync("ratelimit:anonymous:10.1.2.3", Arg.Any<RateLimitPolicy>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OverTheLimit_Returns429WithRetryAfterAndDoesNotCallNext()
    {
        Decide(false, remaining: 0);
        var context = Request("/api/v1/auth/login", "POST");

        await Middleware().InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        context.Response.Headers.RetryAfter.ToString().Should().Be("42");
        context.Response.Headers["X-RateLimit-Remaining"].ToString().Should().Be("0");
        _nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task UnderTheLimit_PassesThroughWithHeaders()
    {
        Decide(true, remaining: 7);
        var context = Request("/api/v1/stock");

        await Middleware().InvokeAsync(context);

        _nextCalled.Should().BeTrue();
        context.Response.Headers["X-RateLimit-Remaining"].ToString().Should().Be("7");
    }

    [Fact]
    public async Task CorsPreflightAndDisabledLimiter_AreNeverCounted()
    {
        await Middleware().InvokeAsync(Request("/api/v1/orders", "OPTIONS"));
        await Middleware(new RateLimitOptions { Enabled = false }).InvokeAsync(Request("/api/v1/orders"));

        await _limiter.DidNotReceiveWithAnyArgs().CheckAsync(default!, default!, default);
    }

    [Fact]
    public async Task RedisDown_FailsOpen()
    {
        var redis = Substitute.For<IConnectionMultiplexer>();
        redis.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_ => throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));

        var decision = await new RedisRateLimiter(redis, NullLogger<RedisRateLimiter>.Instance)
            .CheckAsync("key", new RateLimitPolicy { Limit = 5, WindowSeconds = 60 }, CancellationToken.None);

        decision.Allowed.Should().BeTrue();
    }
}
