namespace Currencies.Infrastructure;

using System.Net;
using System.Net.Http;
using Currencies.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using Serilog;

/// <summary>
/// Provides resilience policies for HTTP requests to the Frankfurter API, including retry and circuit breaker strategies.
/// </summary>
public class ApiResiliencePolicies : IApiResiliencePolicies
{
    /// <summary>
    /// Gets the retry policy for handling transient HTTP errors and rate-limiting responses.
    /// </summary>
    public IAsyncPolicy<HttpResponseMessage> RetryPolicy { get; }
    /// <summary>
    /// Gets the circuit breaker policy for handling repeated failures in HTTP requests.
    /// </summary>
    public IAsyncPolicy<HttpResponseMessage> CircuitBreakerPolicy { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiResiliencePolicies"/> class with configured retry and circuit breaker policies.
    /// </summary>
    /// <param name="options">Configuration options for the Frankfurter API.</param>
    /// <param name="logger">Logger for recording policy-related events and errors.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/>, <paramref name="options.Value"/>, or <paramref name="logger"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when retry or circuit breaker configuration values are invalid.</exception>
    public ApiResiliencePolicies(IOptions<FrankfurterApiConfig> options, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(options, nameof(options));
        ArgumentNullException.ThrowIfNull(options.Value, $"{nameof(options)}.{nameof(options.Value)}");
        ArgumentNullException.ThrowIfNull(logger, nameof(logger));

        FrankfurterApiConfig config = options.Value;

        if (config.RetryPolicy.RetryCount < 0 || config.RetryPolicy.BaseBackoffSeconds < 0)
            throw new ArgumentException("Invalid configuration provided " +
                $"{nameof(config.RetryPolicy.RetryCount)} and {nameof(config.RetryPolicy.BaseBackoffSeconds)} should be >= 0");

        if (config.CircuitBreakerPolicy.BreakDurationMinutes < 1 || config.CircuitBreakerPolicy.FailuresBeforeBreaking < 1)
            throw new ArgumentException("Invalid configuration provided " +
                $"{nameof(config.CircuitBreakerPolicy.BreakDurationMinutes)} and {nameof(config.CircuitBreakerPolicy.FailuresBeforeBreaking)} should be >= 1");

        RetryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount: config.RetryPolicy.RetryCount,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(config.RetryPolicy.BaseBackoffSeconds, attempt)),
                onRetryAsync: async (outcome, timespan, attempt, context) =>
                {
                    logger.Warning("Retry attempt {RetryAttempt} after {TimeSpan} due to {StatusCode} or exception {ExceptionMessage}",
                        attempt, timespan, outcome.Result?.StatusCode, outcome.Exception?.Message);
                    await Task.CompletedTask;
                });

        CircuitBreakerPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: config.CircuitBreakerPolicy.FailuresBeforeBreaking,
                durationOfBreak: TimeSpan.FromMinutes(config.CircuitBreakerPolicy.BreakDurationMinutes),
                onBreak: (outcome, breakDelay) =>
                {
                    logger.Error("Frankfurter Circuit breaker opened for {BreakDelay} due to {StatusCode} or exception {ExceptionMessage}",
                        breakDelay, outcome.Result?.StatusCode, outcome.Exception?.Message);
                },
                onReset: () => logger.Information("Frankfurter Circuit breaker reset."),
                onHalfOpen: () => logger.Information("Frankfurter Circuit breaker half-open."));
    }
}