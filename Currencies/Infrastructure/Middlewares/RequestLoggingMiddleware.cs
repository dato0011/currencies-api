namespace Currencies.Infrastructure.Middlewares;

using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Serilog;
using Serilog.Context;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger logger)
    {
        _next = next;
        _logger = logger;
    }

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

    private void EnrichLogs(HttpContext context, string clientId)
    {
        var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString();
        LogContext.PushProperty("CorrelationId", correlationId);
        LogContext.PushProperty("ClientId", clientId);
        context.Items["CorrelationId"] = correlationId;
        context.Items["ClientId"] = clientId;
    }
}

public static class RequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLoggingMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestLoggingMiddleware>();
    }
}
