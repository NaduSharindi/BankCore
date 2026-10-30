namespace BankCore.Api.Middleware;

/// <summary>OWASP Secure Headers for an API that returns JSON only.</summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";

            if (context.Request.Path.StartsWithSegments("/api"))
            {
                headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
                headers.CacheControl = "no-store";
            }

            return Task.CompletedTask;
        });

        return next(context);
    }
}
