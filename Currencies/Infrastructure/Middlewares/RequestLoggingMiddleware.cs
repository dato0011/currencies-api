namespace Currencies.Infrastructure.Middlewares;

using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Serilog;
using Serilog.Context;

/// <summary>
/// Middleware for logging HTTP request details, including client information, response codes, and response times.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestLoggingMiddleware"/> class.
    /// </summary>
    /// <param name="next">The delegate to the next middleware in the pipeline.</param>
    /// <param name="logger">Logger for recording request details.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="next"/> or <paramref name="logger"/> is null.</exception>
    public RequestLoggingMiddleware(RequestDelegate next, ILogger logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Processes the HTTP request, logs request details, and measures response time.
    /// </summary>
    /// <param name="context">The HTTP context of the current request.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// Logs details such as client IP, client ID (from JWT if authenticated), HTTP method, endpoint, response code, and response time.
    /// </remarks>
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        var clientIp = context.Request.Headers["X-Forwarded-For"].FirstOrDefault() // Are we behind proxy/load balancer?
            ?? context.Connection.RemoteIpAddress?.ToString(); // Nevermind :)
        var httpMethod = context.Request.Method;
        var endpoint = $"{context.Request.Path}{context.Request.QueryString}";
        var clientId = "Anonymous";

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var token = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            if (!string.IsNullOrEmpty(token))
            {
                var handler = new JwtSecurityTokenHandler();
                if (handler.CanReadToken(token))
                {
                    var jwtToken = handler.ReadJwtToken(token);
                    clientId = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value
                        ?? "Unknown";
                }
            }
        }

        EnrichLogs(context, clientId);

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            var responseCode = context.Response.StatusCode;
            var responseTimeMs = stopwatch.ElapsedMilliseconds;

            _logger.Debug(
                "Request: ClientIP={ClientIP}, ClientId={ClientId}, Method={HttpMethod}, Endpoint={Endpoint}, ResponseCode={ResponseCode}, ResponseTimeMs={ResponseTimeMs}",
                clientIp, clientId, httpMethod, endpoint, responseCode, responseTimeMs);
        }
    }

    /// <summary>
    /// Enriches log context with correlation ID and client ID for consistent request tracking.
    /// </summary>
    /// <param name="context">The HTTP context of the current request.</param>
    /// <param name="clientId">The client ID, derived from the JWT or set to "Anonymous" if unauthenticated.</param>
    private void EnrichLogs(HttpContext context, string clientId)
    {
        var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString();
        LogContext.PushProperty("CorrelationId", correlationId);
        LogContext.PushProperty("ClientId", clientId);
        context.Items["CorrelationId"] = correlationId;
        context.Items["ClientId"] = clientId;
    }
}

/// <summary>
/// Extension methods for registering the <see cref="RequestLoggingMiddleware"/> in the application pipeline.
/// </summary>
public static class RequestLoggingMiddlewareExtensions
{
    /// <summary>
    /// Adds the <see cref="RequestLoggingMiddleware"/> to the application pipeline.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The updated application builder.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>    
    public static IApplicationBuilder UseRequestLoggingMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestLoggingMiddleware>();
    }
}
