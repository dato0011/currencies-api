namespace Currencies.Infrastructure.Implementations;

using System.Collections.Generic;
using System.Text.Json;
using Currencies.Infrastructure.Configuration;
using Currencies.Models;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Serilog;

public class FrankfurterRateProvider : ICurrencyRateProvider
{
    private readonly HttpClient _httpClient;
    private readonly FrankfurterApiConfig _config;
    private readonly ILogger _logger;
    private readonly ISimpleCacheProvider _cache;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
    private readonly IAsyncPolicy<HttpResponseMessage> _circuitBreakerPolicy;

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

    private static string BuildQueryParameters(string? @base, string[]? symbols)
    {
        var query = new List<string>(2);
        if (!string.IsNullOrEmpty(@base)) query.Add($"base={Uri.EscapeDataString(@base)}");
        if (symbols is not null && symbols.Length > 0)
            query.Add($"symbols={Uri.EscapeDataString(string.Join(",", symbols))}");

        return query.Any() ? $"?{string.Join("&", query)}" : string.Empty;
    }
}
