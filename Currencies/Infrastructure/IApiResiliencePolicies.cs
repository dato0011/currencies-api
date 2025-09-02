namespace Currencies.Infrastructure;

using Polly;

// TODO: Refactor IApiResiliencePolicies so that instead of returning Retry and CB policies
// It returns a collection of policies. This would allow more granular control of resilience
// policies across all API providers.

/// <summary>
/// Defines resilience policies for handling HTTP requests, including retry and circuit breaker strategies.
/// </summary>
public interface IApiResiliencePolicies
{
    /// <summary>
    /// Gets the retry policy for handling transient HTTP errors and rate-limiting responses.
    /// </summary>
    IAsyncPolicy<HttpResponseMessage> RetryPolicy { get; }

    /// <summary>
    /// Gets the circuit breaker policy for managing repeated failures in HTTP requests.
    /// </summary>    
    IAsyncPolicy<HttpResponseMessage> CircuitBreakerPolicy { get; }
}
