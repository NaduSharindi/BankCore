namespace BankCore.Api.Middleware;

/// <summary>
/// Reads or creates an X-Correlation-ID, returns it in the response and adds it to every log
/// entry and audit row, so a single request can be traced end-to-end during incident analysis.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].ToString();
        var correlationId = IsSafe(incoming) ? incoming : Guid.NewGuid().ToString("N");

        context.Items[ItemKey] = correlationId;
        context.TraceIdentifier = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }

    // Prevents header/log injection through a client-supplied value.
    private static bool IsSafe(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 64
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');
}
