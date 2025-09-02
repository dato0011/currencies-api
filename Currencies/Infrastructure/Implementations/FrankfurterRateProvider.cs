namespace Currencies.Infrastructure.Implementations;

using System.Collections.Generic;
using System.Text.Json;
using Currencies.Infrastructure.Configuration;
using Currencies.Models;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Serilog;

/// <summary>
/// Provides currency exchange rate data from the Frankfurter API, including latest rates, historical rates, and currency conversion.
/// </summary>
public class FrankfurterRateProvider : ICurrencyRateProvider
{
    private readonly HttpClient _httpClient;
    private readonly FrankfurterApiConfig _config;
    private readonly ILogger _logger;
    private readonly ISimpleCacheProvider _cache;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
    private readonly IAsyncPolicy<HttpResponseMessage> _circuitBreakerPolicy;

    /// <summary>
    /// Initializes a new instance of the <see cref="FrankfurterRateProvider"/> class.
    /// </summary>
    /// <param name="httpClientFactory">Factory for creating HTTP clients.</param>
    /// <param name="logger">Logger for recording API-related events.</param>
    /// <param name="cacheProvider">Cache provider for storing API responses.</param>
    /// <param name="options">Configuration options for the Frankfurter API.</param>
    /// <param name="resiliencePolicies">Resilience policies for retry and circuit breaker strategies.</param>
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
    public FrankfurterRateProvider(
        IHttpClientFactory httpClientFactory,
        ILogger logger,
        ISimpleCacheProvider cacheProvider,
        IOptions<FrankfurterApiConfig> options,
        [FromKeyedServices(Constants.DiKeyFraknfurterResiliencePolicy)] IApiResiliencePolicies resiliencePolicies)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory, nameof(httpClientFactory));
        ArgumentNullException.ThrowIfNull(options, nameof(options));
        ArgumentNullException.ThrowIfNull(resiliencePolicies, nameof(resiliencePolicies));
        ArgumentNullException.ThrowIfNull(resiliencePolicies.CircuitBreakerPolicy, nameof(resiliencePolicies.CircuitBreakerPolicy));
        ArgumentNullException.ThrowIfNull(resiliencePolicies.RetryPolicy, nameof(resiliencePolicies.RetryPolicy));

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cacheProvider ?? throw new ArgumentNullException(nameof(cacheProvider));
        _config = options.Value ?? throw new ArgumentNullException(nameof(options));
        _retryPolicy = resiliencePolicies.RetryPolicy;
        _circuitBreakerPolicy = resiliencePolicies.CircuitBreakerPolicy;

        _httpClient = httpClientFactory.CreateClient(Constants.HttpClientThirdPartyApi);
        _httpClient.BaseAddress = new Uri(_config.BaseUrl);
        _cache = cacheProvider;
    }

    /// <summary>
    /// Retrieves the latest currency exchange rates for a specified base currency and optional symbols.
    /// </summary>
    /// <param name="base">The base currency symbol (optional).</param>
    /// <param name="symbols">An array of currency symbols to retrieve rates for (optional).</param>
    /// <returns>A <see cref="CurrencyRate"/> object containing the latest exchange rates.</returns>
    /// <exception cref="ArgumentException">Thrown when the base currency or symbols are invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the API response is invalid.</exception>
    public async Task<CurrencyRate> GetRatesAsync(string? @base = null, string[]? symbols = null)
    {
        if (@base != null && string.IsNullOrWhiteSpace(@base))
            throw new ArgumentException("Base currency cannot be empty or whitespace.", nameof(@base));
        if (symbols != null && symbols.Any(s => string.IsNullOrWhiteSpace(s)))
            throw new ArgumentException("Symbols cannot contain empty or whitespace values.", nameof(symbols));

        var query = BuildQueryParameters(@base, symbols);
        var url = $"/v1/latest{query}";

        var result = await QueryApiAsync<CurrencyRate>(url, TimeSpan.FromMinutes(_config.CacheLatestDurationMinutes));

        if (result is null || result.Rates is null)
            throw new InvalidOperationException("Invalid response from exchange rate service.");

        return result;
    }

    /// <summary>
    /// Retrieves historical currency exchange rates for a specified date range, base currency, and optional symbols.
    /// </summary>
    /// <param name="startDate">The start date for the historical rates.</param>
    /// <param name="endDate">The end date for the historical rates (optional).</param>
    /// <param name="base">The base currency symbol (optional).</param>
    /// <param name="symbols">An array of currency symbols to retrieve rates for (optional).</param>
    /// <returns>A <see cref="HistoricalRates"/> object containing the historical exchange rates.</returns>
    /// <exception cref="ArgumentException">Thrown when the base currency, symbols, or date range are invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the API response is invalid.</exception>
    public async Task<HistoricalRates> GetHistoricalRatesAsync(DateTime startDate, DateTime? endDate, string? @base = null, string[]? symbols = null)
    {
        if (@base != null && string.IsNullOrWhiteSpace(@base))
            throw new ArgumentException("Base currency cannot be empty or whitespace.", nameof(@base));
        if (symbols != null && symbols.Any(s => string.IsNullOrWhiteSpace(s)))
            throw new ArgumentException("Symbols cannot contain empty or whitespace values.", nameof(symbols));
        if (startDate > DateTime.UtcNow)
            throw new ArgumentException("Start date cannot be in the future.", nameof(startDate));
        if (endDate.HasValue && endDate.Value < startDate)
            throw new ArgumentException("End date cannot be earlier than start date.", nameof(endDate));

        var endDateStr = endDate?.ToString("yyyy-MM-dd") ?? string.Empty;
        var dateRange = $"/v1/{startDate:yyyy-MM-dd}..{endDateStr}";
        var query = BuildQueryParameters(@base, symbols);
        var url = $"{dateRange}{query}";

        var result = await QueryApiAsync<HistoricalRates>(url, TimeSpan.FromHours(_config.CacheHistoricalDurationHours));

        if (result is null || result.Rates is null)
            throw new InvalidOperationException("Invalid response from exchange rate service.");

        return result;
    }

    /// <summary>
    /// Converts an amount from one currency to another using the latest exchange rates.
    /// </summary>
    /// <param name="from">The source currency symbol.</param>
    /// <param name="to">The target currency symbol.</param>
    /// <param name="amount">The amount to convert.</param>
    /// <returns>The converted amount rounded to two decimal places.</returns>
    /// <exception cref="ArgumentException">Thrown when the source currency, target currency, or amount is invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no exchange rate is found for the specified currencies.</exception>
    public async Task<decimal> Convert(string from, string to, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(from))
            throw new ArgumentException("Source currency cannot be empty or whitespace.", nameof(from));
        if (string.IsNullOrWhiteSpace(to))
            throw new ArgumentException("Target currency cannot be empty or whitespace.", nameof(to));
        if (amount <= 0)
            throw new ArgumentException("Amount cannot be zero or negative.", nameof(amount));

        var response = await GetRatesAsync(from, new[] { to });
        if (!response.Rates.TryGetValue(to, out var rate))
            throw new InvalidOperationException($"No exchange rate found for {from} to {to}.");

        return Math.Round(amount * rate, 2);
    }

    /// <summary>
    /// Queries the Frankfurter API and caches the response for the specified duration.
    /// </summary>
    /// <typeparam name="T">The type of the response model, which must implement <see cref="ModelWithExpiration"/>.</typeparam>
    /// <param name="url">The API endpoint URL to query.</param>
    /// <param name="cacheDuration">The duration for which the response should be cached.</param>
    /// <returns>The deserialized API response of type <typeparamref name="T"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the API response cannot be deserialized.</exception>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails.</exception>
    /// <exception cref="BrokenCircuitException">Thrown when the circuit breaker is open.</exception>
    private async Task<T> QueryApiAsync<T>(string url, TimeSpan cacheDuration) where T : ModelWithExpiration
    {
        var cacheKey = $"{_config.BaseUrl}/{url}";
        var cachedData = await _cache.GetCachedDataAsync<T>(cacheKey);
        if (cachedData != null) return cachedData;

        _logger.Information("Fetching historical rates from {Url}", url);

        var policyWrap = Policy.WrapAsync(_retryPolicy, _circuitBreakerPolicy);

        HttpResponseMessage response;
        try
        {
            response = await policyWrap.ExecuteAsync(() => _httpClient.GetAsync(url));
        }
        catch (BrokenCircuitException)
        {
            // TODO: Increment Frankfurter API outage metric
            throw;
        }
        catch (HttpRequestException)
        {
            // TODO: Increment Fraknfurter API error metric
            throw;
        }
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (data == null)
        {
            _logger.Debug("Failed to deserialize API response from {Url}: {Json}", url, json);
            throw new InvalidOperationException("Failed to deserialize API response.");
        }

        await _cache.SetCacheDataAsync(cacheKey, data with { Expires = DateTime.UtcNow + cacheDuration });

        return data;
    }

    /// <summary>
    /// Builds the query string parameters for API requests based on the base currency and symbols.
    /// </summary>
    /// <param name="base">The base currency symbol (optional).</param>
    /// <param name="symbols">An array of currency symbols (optional).</param>
    /// <returns>A formatted query string for the API request.</returns>
    private static string BuildQueryParameters(string? @base, string[]? symbols)
    {
        var query = new List<string>(2);
        if (!string.IsNullOrEmpty(@base)) query.Add($"base={Uri.EscapeDataString(@base)}");
        if (symbols is not null && symbols.Length > 0)
            query.Add($"symbols={Uri.EscapeDataString(string.Join(",", symbols))}");

        return query.Any() ? $"?{string.Join("&", query)}" : string.Empty;
    }
}
