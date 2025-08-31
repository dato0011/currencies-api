namespace Currencies.Infrastructure;

using System.Net;
using System.Net.Http;
using Currencies.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using Serilog;

public class ApiResiliencePolicies : IApiResiliencePolicies
{
    public IAsyncPolicy<HttpResponseMessage> RetryPolicy { get; }
    public IAsyncPolicy<HttpResponseMessage> CircuitBreakerPolicy { get; }

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