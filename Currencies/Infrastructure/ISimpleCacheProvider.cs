namespace Currencies.Infrastructure;

using Currencies.Models;

public interface ISimpleCacheProvider
{
    Task<T?> GetCachedDataAsync<T>(string url) where T : ModelWithExpiration;
    Task SetCacheDataAsync<T>(string url, T data) where T : ModelWithExpiration;
}
