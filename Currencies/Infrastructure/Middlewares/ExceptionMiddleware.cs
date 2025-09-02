namespace Currencies.Infrastructure.Middlewares;

using Polly.CircuitBreaker;
using Serilog;
using System.Net;
using System.Text.Json;

/// <summary>
/// Middleware for catching and handling unhandled exceptions globally, logging them, and returning standardized error responses.
/// </summary>
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExceptionMiddleware"/> class.
    /// </summary>
    /// <param name="next">The delegate to the next middleware in the pipeline.</param>
    /// <param name="logger">Logger for recording exception-related events.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="next"/> or <paramref name="logger"/> is null.</exception>    
    public ExceptionMiddleware(RequestDelegate next, ILogger logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes the HTTP request, catching and handling any unhandled exceptions.
    /// </summary>
    /// <param name="context">The HTTP context of the current request.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// Catches <see cref="BrokenCircuitException"/> to return a 503 Service Unavailable response, and all other exceptions to return a 500 Internal Server Error response.
    /// </remarks>
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

    /// <summary>
    /// Handles an exception by logging it and writing a standardized JSON error response.
    /// </summary>
    /// <param name="context">The HTTP context of the current request.</param>
    /// <param name="ex">The exception to handle.</param>
    /// <param name="statusCode">The HTTP status code for the response (default: 500 Internal Server Error).</param>
    /// <param name="message">The error message to include in the response (default: generic error message).</param>
    /// <returns>A task representing the asynchronous operation of writing the error response.</returns>
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

/// <summary>
/// Extension methods for registering the <see cref="ExceptionMiddleware"/> in the application pipeline.
/// </summary>
public static class ExceptionMiddlewareExtensions
{
    /// <summary>
    /// Adds the <see cref="ExceptionMiddleware"/> to the application pipeline.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The updated application builder.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>    
    public static IApplicationBuilder UseExceptionMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ExceptionMiddleware>();
    }
}
