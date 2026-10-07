namespace OrderFlow.Gateway;

/// <summary>Basic browser protections for every response. The gateway only returns JSON, so the CSP can be fully closed.</summary>
public class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            headers["Cross-Origin-Resource-Policy"] = "same-site";

            // Proxied responses carry the downstream "Server: Kestrel"; do not advertise what runs behind the gateway.
            headers.Remove("Server");
            return Task.CompletedTask;
        });

        return next(context);
    }
}
