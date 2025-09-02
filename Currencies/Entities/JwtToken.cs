namespace Currencies.Entities;

/// <summary>
/// Represents a JWT token pair containing an access token and a refresh token, along with their respective expiration dates.
/// </summary>
public record JwtToken
{
    /// <summary>
    /// Gets the JWT access token used for authenticating API requests.
    /// </summary>
    public required string AccessToken { get; init; }

    /// <summary>
    /// Gets the refresh token used to obtain a new access token when the current one expires.
    /// </summary>
    public required string RefreshToken { get; init; }

    /// <summary>
    /// Gets the date and time when the access token expires.
    /// </summary>
    public required DateTime AccessTokenExpiration { get; init; }

    /// <summary>
    /// Gets the date and time when the refresh token expires.
    /// </summary>
    public required DateTime RefreshTokenExpiration { get; init; }
}
