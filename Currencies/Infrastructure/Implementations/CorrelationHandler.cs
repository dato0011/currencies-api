/// <summary>
/// An HTTP message handler that injects a correlation ID header into outgoing requests.
/// </summary>
/// <remarks>
/// The correlation ID is retrieved from the current <see cref="HttpContext"/> via
/// <see cref="IHttpContextAccessor"/> and added as the <c>X-Correlation-ID</c> header.
/// This enables distributed tracing across service boundaries.
/// </remarks>
public class CorrelationHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="CorrelationHandler"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">
    /// The accessor used to obtain the current <see cref="HttpContext"/>,
    /// from which the correlation ID is read.
    /// </param>
    public CorrelationHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Sends the HTTP request with an <c>X-Correlation-ID</c> header if a correlation ID
    /// is available in the current request context.
    /// </summary>
    /// <param name="request">The outgoing HTTP request message.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task representing the asynchronous operation, containing the HTTP response message.
    /// </returns>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var correlationId = _httpContextAccessor?.HttpContext?.Items["CorrelationId"]?.ToString();
        if (!string.IsNullOrEmpty(correlationId))
        {
            request.Headers.Add("X-Correlation-ID", correlationId);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
