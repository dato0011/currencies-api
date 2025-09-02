namespace Currencies.Repositories;

using Currencies.Entities;

/// <summary>
/// Defines methods for retrieving and managing user authentication and token information.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Retrieves a user by their username.
    /// </summary>
    /// <param name="username">The username of the user to retrieve.</param>
    /// <returns>The <see cref="User"/> object if found; otherwise, <c>null</c>.</returns>
    User? GetUser(string username);

    /// <summary>
    /// Retrieves a user by their username and password hash.
    /// </summary>
    /// <param name="username">The username of the user to retrieve.</param>
    /// <param name="hash">The password hash to verify against the user's stored password hash.</param>
    /// <returns>The <see cref="User"/> object if the username and password hash match; otherwise, <c>null</c>.</returns>
    User? GetUser(string username, string hash);

    /// <summary>
    /// Retrieves a user associated with a given refresh token.
    /// </summary>
    /// <param name="refreshToken">The refresh token to look up.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation, containing the <see cref="User"/> object if the token is valid and associated with a user; otherwise, <c>null</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="refreshToken"/> is null or empty.</exception>
    Task<User?> GetUserByRefreshTokenAsync(string refreshToken);

    /// <summary>
    /// Retrieves a user associated with a given access token.
    /// </summary>
    /// <param name="accessToken">The access token to look up.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation, containing the <see cref="User"/> object if the token is valid and associated with a user; otherwise, <c>null</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="accessToken"/> is null or empty.</exception>
    Task<User?> GetUserByAccessTokenAsync(string accessToken);

    /// <summary>
    /// Stores a refresh token for a user with an expiration date.
    /// </summary>
    /// <param name="username">The username associated with the token.</param>
    /// <param name="refreshToken">The refresh token to store.</param>
    /// <param name="expires">The expiration date and time of the token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="username"/> or <paramref name="refreshToken"/> is null or empty.</exception>
    Task StoreRefreshTokenAsync(string username, string refreshToken, DateTime expires);

    /// <summary>
    /// Validates a refresh token and retrieves the associated user if the token is valid and not expired.
    /// </summary>
    /// <param name="refreshToken">The refresh token to validate.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation, containing the <see cref="User"/> object if the token is valid; otherwise, <c>null</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="refreshToken"/> is null or empty.</exception>
    Task<User?> ValidateRefreshTokenAsync(string refreshToken);

    /// <summary>
    /// Stores an access token for a user with an expiration date.
    /// </summary>
    /// <param name="username">The username associated with the token.</param>
    /// <param name="accessToken">The access token to store.</param>
    /// <param name="expires">The expiration date and time of the token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="username"/> or <paramref name="accessToken"/> is null or empty.</exception>
    Task StoreAccessTokenAsync(string username, string accessToken, DateTime expires);

    /// <summary>
    /// Validates an access token and retrieves the associated user if the token is valid and not expired.
    /// </summary>
    /// <param name="accessToken">The access token to validate.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation, containing the <see cref="User"/> object if the token is valid; otherwise, <c>null</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="accessToken"/> is null or empty.</exception>
    Task<User?> ValidateAccessTokenAsync(string accessToken);

    /// <summary>
    /// Revokes an access token, making it invalid for further use.
    /// </summary>
    /// <param name="accessToken">The access token to revoke.</param>
    /// <param name="username">The username associated with the token. If null, the username is retrieved from the token mapping.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="accessToken"/> is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the user associated with the token cannot be found and <paramref name="username"/> is null.</exception>
    Task RevokeAccessTokenAsync(string accessToken, string? username = null);

    /// <summary>
    /// Revokes a refresh token, making it invalid for further use.
    /// </summary>
    /// <param name="refreshToken">The refresh token to revoke.</param>
    /// <param name="username">The username associated with the token. If null, the username is retrieved from the token mapping.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="refreshToken"/> is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the user associated with the token cannot be found and <paramref name="username"/> is null.</exception>
    Task RevokeRefreshTokenAsync(string refreshToken, string? username = null);
}
