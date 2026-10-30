using BankCore.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BankCore.Api.Middleware;

/// <summary>
/// Converts exceptions into RFC 9457 ProblemDetails responses. Internal details (stack traces,
/// SQL errors) are logged but never returned to the client.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, errorCode) = exception switch
        {
            NotFoundException e => (StatusCodes.Status404NotFound, "Resource not found", e.ErrorCode),
            ConflictException e => (StatusCodes.Status409Conflict, "Conflict", e.ErrorCode),
            AuthenticationFailedException e => (StatusCodes.Status401Unauthorized, "Authentication failed", e.ErrorCode),
            InsufficientFundsException e => (StatusCodes.Status422UnprocessableEntity, "Insufficient funds", e.ErrorCode),
            DomainException e => (StatusCodes.Status422UnprocessableEntity, "Business rule violation", e.ErrorCode),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "Bad request", "BAD_REQUEST"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred", "INTERNAL_ERROR"),
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception on {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogWarning("Request rejected with {ErrorCode}: {Message}", errorCode, exception.Message);
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status >= StatusCodes.Status500InternalServerError
                ? "Please contact support and quote the correlationId."
                : exception.Message,
            Instance = httpContext.Request.Path,
        };
        problem.Extensions["errorCode"] = errorCode;
        problem.Extensions["correlationId"] =
            httpContext.Items[CorrelationIdMiddleware.ItemKey] as string ?? httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
