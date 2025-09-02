namespace Currencies.Infrastructure.Configuration;

/// <summary>
/// Configuration settings for JSON Web Token (JWT) authentication.
/// </summary>
public class JwtSettings
{
    /// <summary>
    /// The secret key used for signing JWT tokens.
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// The issuer claim (iss) to be embedded in generated tokens,
    /// typically identifying the authorization server.
    /// </summary>
    public required string Issuer { get; set; }

    /// <summary>
    /// The audience claim (aud) to be embedded in generated tokens,
    /// typically identifying the intended recipients (e.g., API consumers).
    /// </summary>
    public required string Audience { get; set; }

    /// <summary>
    /// The lifetime of access tokens, in minutes, before expiration.
    /// </summary>
    public required int AccessExpireMinutes { get; set; }

    /// <summary>
    /// The lifetime of refresh tokens, in days, before expiration.
    /// </summary>
    public required int RefreshExpireDays { get; set; }
}
