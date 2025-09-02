namespace Currencies.Infrastructure.Configuration;

/// <summary>
/// Configuration settings for the Frankfurter currency exchange API integration.
/// </summary>
public class FrankfurterApiConfig
{
    /// <summary>
    /// The base URL endpoint of the Frankfurter API (e.g., "https://api.frankfurter.app").
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// The cache lifetime, in minutes, for the latest exchange rate requests.
    /// </summary>
    public int CacheLatestDurationMinutes { get; set; }

    /// <summary>
    /// The cache lifetime, in hours, for historical exchange rate requests.
    /// </summary>
    public int CacheHistoricalDurationHours { get; set; }

    /// <summary>
    /// Retry policy configuration applied to failed API calls.
    /// </summary>
    public RetryPolicyConfig RetryPolicy { get; set; } = new();

    /// <summary>
    /// Circuit breaker policy configuration applied to protect against repeated API failures.
    /// </summary>
    public CircuitBreakerPolicyConfig CircuitBreakerPolicy { get; set; } = new();
}

/// <summary>
/// Configuration for retry policy behavior when API calls fail.
/// </summary>
public class RetryPolicyConfig
{
    /// <summary>
    /// The maximum number of retry attempts to perform after an API call failure.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// The initial delay, in seconds, used as a base for exponential backoff between retries.
    /// </summary>
    public int BaseBackoffSeconds { get; set; }
}

/// <summary>
/// Configuration for circuit breaker behavior to handle repeated failures gracefully.
/// </summary>
public class CircuitBreakerPolicyConfig
{
    /// <summary>
    /// The number of consecutive failures required before the circuit transitions to the "open" state.
    /// </summary>
    public int FailuresBeforeBreaking { get; set; }

    /// <summary>
    /// The duration, in minutes, the circuit remains open before allowing attempts again.
    /// </summary>
    public int BreakDurationMinutes { get; set; }
}
