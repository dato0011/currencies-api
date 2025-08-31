namespace Currencies.Infrastructure.Implementations;

/// <summary>
/// This class ensures all outgoing HTTP requests contains 'X-Correlation-ID' 
/// header corresponding to inbound request's header. 
/// </summary>
public class CorrelationHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CorrelationHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var correlationId = _httpContextAccessor?.HttpContext?.Items["CorrelationId"]?.ToString();
        if (!string.IsNullOrEmpty(correlationId))
        {
            request.Headers.Add("X-Correlation-ID", correlationId);
        }
        return await base.SendAsync(request, cancellationToken);
    }
}
