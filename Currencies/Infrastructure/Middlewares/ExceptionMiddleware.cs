namespace Currencies.Infrastructure.Middlewares;

using Polly.CircuitBreaker;
using Serilog;
using System.Net;
using System.Text.Json;

/// <summary>
/// Middleware to handle and log exceptions globally and return standardized error responses
/// </summary>
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger _logger;

    /// <summary>
    /// Constructor to initialize the middleware with the next delegate and logger
    /// </summary>
    /// <param name="next">Next delegate</param>
    /// <param name="logger">Logger instance</param>
    public ExceptionMiddleware(RequestDelegate next, ILogger logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Invoke the middleware. The middleware will invoke the next delegate and catch any unhandled exceptions.
    /// </summary>
    /// <param name="context">An instance of <see cref="HttpContext"/> object</param>
    /// <returns></returns>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (BrokenCircuitException ex)
        {
            await HandleExceptionAsync(context, ex, HttpStatusCode.ServiceUnavailable,
                "Service temporarily unavailable. Please try again later");
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private Task HandleExceptionAsync(
        HttpContext context,
        Exception ex, 
        HttpStatusCode statusCode = HttpStatusCode.InternalServerError,
        string message = "An unexpected error occurred. Please try again later.")
    {
        _logger.Error(ex, "Unhandled exception in {Method} {Path}",
            context.Request.Method, context.Request.Path);

        var response = new
        {
            StatusCode = (int)statusCode,
            Message = message
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        return context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}

public static class ExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ExceptionMiddleware>();
    }
}
