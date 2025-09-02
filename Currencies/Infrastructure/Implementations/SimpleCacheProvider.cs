namespace Currencies.Infrastructure.Implementations;

using System.Text.Json;
using Currencies.Models;
using Microsoft.Extensions.Caching.Distributed;
using Serilog;

/// <summary>
/// Provides caching functionality for storing and retrieving data using a distributed cache.
/// </summary>
public class SimpleCacheProvider : ISimpleCacheProvider
{
    private const string CachePrefix = "cache:";

    private readonly ILogger _logger;
    private readonly IDistributedCache _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleCacheProvider"/> class.
    /// </summary>
    /// <param name="logger">Logger for recording cache operations and events.</param>
    /// <param name="cache">Distributed cache instance for data storage.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> or <paramref name="cache"/> is null.</exception>
    public SimpleCacheProvider(ILogger logger, IDistributedCache cache)
    {
        ArgumentNullException.ThrowIfNull(logger, nameof(logger));
        ArgumentNullException.ThrowIfNull(cache, nameof(cache));

        _logger = logger;
        _cache = cache;
    }

    /// <summary>
    /// Retrieves cached data for a specified URL if it exists and has not expired.
    /// </summary>
    /// <typeparam name="T">The type of the cached data, which must implement <see cref="ModelWithExpiration"/>.</typeparam>
    /// <param name="url">The URL used to generate the cache key.</param>
    /// <returns>The cached data of type <typeparamref name="T"/> if available and not expired; otherwise, null.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="url"/> is null or whitespace.</exception>
    public async Task<T?> GetCachedDataAsync<T>(string url) where T : ModelWithExpiration
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url, nameof(url));

        var key = $"{CachePrefix}:{url.GetHashCode()}";
        var cachedData = await _cache.GetStringAsync(key);
        if (string.IsNullOrWhiteSpace(cachedData))
        {
            _logger.Debug("Cache miss for key {Key} ({url})", key, url);
            // TODO: Increment cache miss metric
            return null;
        }

        var result = JsonSerializer.Deserialize<T>(cachedData);
        if (result is null || result.Expires < DateTime.UtcNow)
        {
            _logger.Debug("Cache expired for key {Key} ({url})", key, url);
            return null;
            // TODO: Increment expired cache hit metric
        }

        // TODO: Increment cache hit metric

        _logger.Debug("Cache hit for key {key} ({Url})", key, url);

        return result;
    }

    /// <summary>
    /// Stores data in the cache with a specified expiration time.
    /// </summary>
    /// <typeparam name="T">The type of the data to cache, which must implement <see cref="ModelWithExpiration"/>.</typeparam>
    /// <param name="url">The URL used to generate the cache key.</param>
    /// <param name="data">The data to cache.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="url"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
    public async Task SetCacheDataAsync<T>(string url, T data) where T : ModelWithExpiration
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentNullException.ThrowIfNull(data);

        var key = $"{CachePrefix}:{url.GetHashCode()}";
        var json = JsonSerializer.Serialize(data);

        await Cache(key, json, data.Expires);
    }

    /// <summary>
    /// Caches a string value with the specified key and expiration time.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="data">The data to cache.</param>
    /// <param name="expiresUtc">The UTC expiration time for the cached data.</param>
    private async Task Cache(string key, string data, DateTime expiresUtc)
    {
        _logger.Debug("Caching {Type} at {Key}", data.GetType().Name, key);

        await _cache.SetStringAsync(key, data, new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = expiresUtc
        });
    }
}
