namespace Currencies.Infrastructure;

using Currencies.Models;

/// <summary>
/// Defines the contract for a caching provider that handles storing and retrieving data with expiration.
/// </summary>
public interface ISimpleCacheProvider
{
    /// <summary>
    /// Retrieves cached data for a specified URL if it exists and has not expired.
    /// </summary>
    /// <typeparam name="T">The type of the cached data, which must implement <see cref="ModelWithExpiration"/>.</typeparam>
    /// <param name="url">The URL used to generate the cache key.</param>
    /// <returns>A <see cref="Task{TResult}"/> containing the cached data of type <typeparamref name="T"/> if available and not expired; otherwise, null.</returns>    
    Task<T?> GetCachedDataAsync<T>(string url) where T : ModelWithExpiration;

    /// <summary>
    /// Stores data in the cache with a specified expiration time.
    /// </summary>
    /// <typeparam name="T">The type of the data to cache, which must implement <see cref="ModelWithExpiration"/>.</typeparam>
    /// <param name="url">The URL used to generate the cache key.</param>
    /// <param name="data">The data to cache.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>    
    Task SetCacheDataAsync<T>(string url, T data) where T : ModelWithExpiration;
}
