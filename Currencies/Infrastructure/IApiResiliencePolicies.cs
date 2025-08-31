namespace Currencies.Infrastructure;

using Polly;

public interface IApiResiliencePolicies
{
    IAsyncPolicy<HttpResponseMessage> RetryPolicy { get; }
    IAsyncPolicy<HttpResponseMessage> CircuitBreakerPolicy { get; }
}
