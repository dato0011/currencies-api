namespace Currencies.Repositories.Implementations;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Currencies.Entities;
using Microsoft.Extensions.Caching.Distributed;

internal record TokenData(string Token, string Username, DateTime Expires);

/// <summary>
/// Provides methods for managing users and handling authentication tokens.
/// </summary>
public class UserRepository : IUserRepository
{
    private const string RTOKEN = "refresh_token";
    private const string ATOKEN = "access_token";
    private const string ATOKEN_USER_MAP = "access_token_to_user_map";
    private const string RTOKEN_USER_MAP = "refresh_token_to_user_map";

    private readonly IDistributedCache _cache;
    private readonly HashAlgorithm _hash;

    private readonly List<User> _users = // Masterpiece (⌐■_■)
    [
        new User { Id = 1, Username = "admin", PasswordHash = "111", Role = "Admin" },
        new User { Id = 2, Username = "user", PasswordHash = "222", Role = "User" }
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="UserRepository"/> class with the specified cache and hash algorithm.
    /// </summary>
    /// <param name="cache">The distributed cache to use for storing tokens.</param>
    /// <param name="hash">The hash algorithm to use for token hashing.</param>
    public UserRepository(IDistributedCache cache, HashAlgorithm hash)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(hash);

        _cache = cache;
        _hash = hash;
    }

    /// <summary>
    /// Retrieves a user by their username.
    /// </summary>
    /// <param name="username">The username of the user to retrieve.</param>
    /// <returns>The <see cref="User"/> object if found; otherwise, <c>null</c>.</returns>
    public User? GetUser(string username)
    {
        return _users.FirstOrDefault(u => u.Username == username);
    }

    /// <summary>
    /// Retrieves a user by their username and password hash.
    /// </summary>
    /// <param name="username">The username of the user to retrieve.</param>
    /// <param name="hash">The password hash to verify against the user's stored password hash.</param>
    /// <returns>The <see cref="User"/> object if the username and password hash match; otherwise, <c>null</c>.</returns>
    public User? GetUser(string username, string hash)
    {
        return _users.FirstOrDefault(u => u.Username == username && u.PasswordHash == hash);
    }

    /// <summary>
    /// Retrieves a user associated with a given refresh token.
    /// </summary>
    /// <param name="refreshToken">The refresh token to look up.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation, containing the <see cref="User"/> object if the token is valid and associated with a user; otherwise, <c>null</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="refreshToken"/> is null or empty.</exception>
    public async Task<User?> GetUserByRefreshTokenAsync(string refreshToken)
    {
        return await GetUserByTokenAsync(refreshToken, false);
    }

    /// <summary>
    /// Retrieves a user associated with a given access token.
    /// </summary>
    /// <param name="accessToken">The access token to look up.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation, containing the <see cref="User"/> object if the token is valid and associated with a user; otherwise, <c>null</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="accessToken"/> is null or empty.</exception>
    public async Task<User?> GetUserByAccessTokenAsync(string accessToken)
    {
        return await GetUserByTokenAsync(accessToken, true);
    }

    /// <summary>
    /// Revokes an access token, removing it from the cache.
    /// </summary>
    /// <param name="accessToken">The access token to revoke.</param>
    /// <param name="username">The username associated with the token. If null, the username is retrieved from the token mapping.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="accessToken"/> is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the user associated with the token cannot be found.</exception>
    public async Task RevokeAccessTokenAsync(string accessToken, string? username = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameof(accessToken), accessToken);

        await RevokeAccessTokenAsync(accessToken, true, username);
    }

    /// <summary>
    /// Revokes a refresh token, removing it from the cache.
    /// </summary>
    /// <param name="refreshToken">The refresh token to revoke.</param>
    /// <param name="username">The username associated with the token. If null, the username is retrieved from the token mapping.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="refreshToken"/> is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the user associated with the token cannot be found.</exception>
    public async Task RevokeRefreshTokenAsync(string refreshToken, string? username = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameof(refreshToken), refreshToken);

        await RevokeAccessTokenAsync(refreshToken, false, username);
    }

    /// <summary>
    /// Stores an access token for a user in the cache with an expiration date.
    /// </summary>
    /// <param name="username">The username associated with the token.</param>
    /// <param name="accessToken">The access token to store.</param>
    /// <param name="expires">The expiration date and time of the token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="username"/> or <paramref name="accessToken"/> is null or empty.</exception>
    public async Task StoreAccessTokenAsync(string username, string accessToken, DateTime expires)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameof(username), username);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameof(accessToken), accessToken);

        await StoreTokenAsync(username, accessToken, expires, true);
    }

    /// <summary>
    /// Stores a refresh token for a user in the cache with an expiration date.
    /// </summary>
    /// <param name="username">The username associated with the token.</param>
    /// <param name="refreshToken">The refresh token to store.</param>
    /// <param name="expires">The expiration date and time of the token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="username"/> or <paramref name="refreshToken"/> is null or empty.</exception>
    public async Task StoreRefreshTokenAsync(string username, string refreshToken, DateTime expires)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameof(username), username);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameof(refreshToken), refreshToken);

        await StoreTokenAsync(username, refreshToken, expires, false);
    }

    /// <summary>
    /// Validates an access token and retrieves the associated user if the token is valid and not expired.
    /// </summary>
    /// <param name="accessToken">The access token to validate.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation, containing the <see cref="User"/> object if the token is valid; otherwise, <c>null</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="accessToken"/> is null or empty.</exception>
    public async Task<User?> ValidateAccessTokenAsync(string accessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameof(accessToken), accessToken);

        return await ValidateToken(accessToken, true);
    }

    /// <summary>
    /// Validates a refresh token and retrieves the associated user if the token is valid and not expired.
    /// </summary>
    /// <param name="refreshToken">The refresh token to validate.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation, containing the <see cref="User"/> object if the token is valid; otherwise, <c>null</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="refreshToken"/> is null or empty.</exception>
    public async Task<User?> ValidateRefreshTokenAsync(string refreshToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameof(refreshToken), refreshToken);

        return await ValidateToken(refreshToken, false);
    }

    private async Task<User?> GetUserByTokenAsync(string token, bool isAccessToken, string? hashedToken = null)
    {
        hashedToken = hashedToken ?? HashToken(token);
        var mapKey = isAccessToken ? $"{ATOKEN_USER_MAP}:{hashedToken}" : $"{RTOKEN_USER_MAP}:{hashedToken}";
        var username = await _cache.GetStringAsync(mapKey);
        if (string.IsNullOrEmpty(username)) return null; // Token not found

        // Verify token validity and expiration
        var key = isAccessToken ? $"{ATOKEN}:{username}:{hashedToken}" : $"{RTOKEN}:{username}:{hashedToken}";
        var cachedData = await _cache.GetStringAsync(key);
        if (string.IsNullOrEmpty(cachedData)) return null; // Token not found or expired

        var tokenData = JsonSerializer.Deserialize<TokenData>(cachedData);
        if (tokenData is null || tokenData.Token != hashedToken || tokenData.Expires <= DateTime.UtcNow)
        {
            return null;
        }

        return _users.First(u => u.Username == username);
    }

    private async Task StoreTokenAsync(string username, string token, DateTime expires, bool isAccessToken)
    {
        var hashedToken = HashToken(token);
        var tokenKey = isAccessToken ? $"{ATOKEN}:{username}:{hashedToken}" : $"{RTOKEN}:{username}:{hashedToken}";
        var mapKey = isAccessToken ? $"{ATOKEN_USER_MAP}:{hashedToken}" : $"{RTOKEN_USER_MAP}:{hashedToken}";
        var tokenData = new TokenData(hashedToken, username, expires);
        var serializedData = JsonSerializer.Serialize(tokenData);
        var tasks = new[]
        {
            _cache.SetStringAsync(tokenKey, serializedData, new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = expires
            }),

            // Store reverse mapping as well to avoid expensive pattern matching queries
            _cache.SetStringAsync(mapKey, username, new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = expires
            })
        };

        // Aggregate both tasks to prevent extra context switches
        await Task.WhenAll(tasks);
    }

    private async Task<User?> ValidateToken(string token, bool isAccessToken)
    {
        var user = await GetUserByTokenAsync(token, isAccessToken);
        if (user is null) return null;

        var hashedToken = HashToken(token);
        var key = isAccessToken ? $"{ATOKEN}:{user.Username}:{hashedToken}" : $"{RTOKEN}:{user.Username}:{hashedToken}";
        var cachedData = await _cache.GetStringAsync(key);
        if (string.IsNullOrEmpty(cachedData)) return null;

        var tokenData = JsonSerializer.Deserialize<TokenData>(cachedData);
        if (tokenData is null || tokenData.Token != hashedToken || tokenData.Expires <= DateTime.UtcNow) return null;

        return user;
    }

    private async Task RevokeAccessTokenAsync(string token, bool isAccessToken, string? username = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameof(token), token);

        var hashedToken = HashToken(token);
        if (string.IsNullOrWhiteSpace(username))
        {
            var user = await GetUserByTokenAsync(token, isAccessToken, hashedToken)
                ?? throw new InvalidOperationException($"Cannot find user for the given access token {hashedToken}");
            username = user.Username;
        }

        var tokenKey = isAccessToken ? $"{ATOKEN}:{username}:{hashedToken}" : $"{RTOKEN}:{username}:{hashedToken}";
        var mapKey = isAccessToken ? $"{ATOKEN_USER_MAP}:{hashedToken}" : $"{RTOKEN_USER_MAP}:{hashedToken}";
        var tasks = new[]
        {
            _cache.RemoveAsync(tokenKey),
            _cache.RemoveAsync(mapKey)
        };

        // Aggregate both tasks to prevent extra context switches
        await Task.WhenAll(tasks);
    }

    private string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = _hash.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
