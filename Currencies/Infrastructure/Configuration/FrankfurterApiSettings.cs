namespace Currencies.Infrastructure.Configuration;

public class FrankfurterApiConfig
{
    public string BaseUrl { get; set; } = string.Empty;
    public int CacheLatestDurationMinutes { get; set; }
    public int CacheHistoricalDurationHours { get; set; }
    public RetryPolicyConfig RetryPolicy { get; set; } = new();
    public CircuitBreakerPolicyConfig CircuitBreakerPolicy { get; set; } = new();
}

public class RetryPolicyConfig
{
    public int RetryCount { get; set; }
    public int BaseBackoffSeconds { get; set; }
}

public class CircuitBreakerPolicyConfig
{
    public int FailuresBeforeBreaking { get; set; }
    public int BreakDurationMinutes { get; set; }
}
