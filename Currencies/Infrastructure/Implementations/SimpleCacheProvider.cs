namespace Currencies.Infrastructure.Implementations;

using System.Text.Json;
using Currencies.Models;
using Microsoft.Extensions.Caching.Distributed;
using Serilog;

public class SimpleCacheProvider : ISimpleCacheProvider
{
    private const string CachePrefix = "cache:";

    private readonly ILogger _logger;
    private readonly IDistributedCache _cache;

    public SimpleCacheProvider(ILogger logger, IDistributedCache cache)
    {
        ArgumentNullException.ThrowIfNull(logger, nameof(logger));
        ArgumentNullException.ThrowIfNull(cache, nameof(cache));

        _logger = logger;
        _cache = cache;
    }

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

    public async Task SetCacheDataAsync<T>(string url, T data) where T : ModelWithExpiration
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentNullException.ThrowIfNull(data);

        var key = $"{CachePrefix}:{url.GetHashCode()}";
        var json = JsonSerializer.Serialize(data);

        await Cache(key, json, data.Expires);
    }

    private async Task Cache(string key, string data, DateTime expiresUtc)
    {
        _logger.Debug("Caching {Type} at {Key}", data.GetType().Name, key);

        await _cache.SetStringAsync(key, data, new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = expiresUtc
        });
    }
}
