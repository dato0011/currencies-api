namespace Currencies.Infrastructure.Implementations;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Serilog;
using Currencies.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using Currencies.Entities;

/// <summary>
/// Factory class for creating JWT tokens based on existing claims
/// </summary>
public class JwtTokenFactory : IJwtTokenFactory
{
    private const int ACCESS_EXPIRES_WARNING_THRESHOLD_MINUTES = 45;
    private const int REFRESH_EXPIRES_WARNING_THRESHOLD_DAYS = 14;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="JwtTokenFactory"/> class.
    /// </summary>
    /// <param name="jwtSettings">The JWT settings configuration.</param>
    /// <param name="logger">The logger instance.</param>
    public JwtTokenFactory(IOptions<JwtSettings> jwtSettings, ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jwtSettings = jwtSettings?.Value ?? throw new ArgumentNullException(nameof(jwtSettings), "JWT settings are not configured properly");
        if (string.IsNullOrWhiteSpace(_jwtSettings.Key) ||
            string.IsNullOrWhiteSpace(_jwtSettings.Issuer) ||
            string.IsNullOrWhiteSpace(_jwtSettings.Audience))
        {
            throw new ArgumentException("JWT settings must include Key, Issuer, and Audience");
        }

        if (_jwtSettings.AccessExpireMinutes > ACCESS_EXPIRES_WARNING_THRESHOLD_MINUTES)
        {
            _logger.Warning("JWT token expiration is set to {ExpireMinutes} minutes, which is higher than the recommended " +
                "threshold of {Threshold} minutes. Consider decreasing the expiration time for better security.",
                _jwtSettings.AccessExpireMinutes, ACCESS_EXPIRES_WARNING_THRESHOLD_MINUTES);
        }
        if (_jwtSettings.RefreshExpireDays > REFRESH_EXPIRES_WARNING_THRESHOLD_DAYS)
        {
            _logger.Warning("JWT refresh token expiration is set to {RefreshExpireDays} days, which is higher than the recommended " +
                "threshold of {Threshold} days. Consider decreasing the refresh expiration time for better security.",
                _jwtSettings.RefreshExpireDays, REFRESH_EXPIRES_WARNING_THRESHOLD_DAYS);
        }
    }

    /// <summary>
    /// Creates a JWT token with the provided claims
    /// </summary>
    /// <param name="claims">List of claims</param>
    /// <returns>An instance of <see cref="JwtSecurityToken"/></returns>
    /// <exception cref="InvalidOperationException">Thrown when JWT parameters are missing from configuration</exception>
    /// /// <exception cref="ArgumentNullException">Thrown when null is provided instead of actuall parameters</exception>
    public JwtToken CreateToken(IEnumerable<Claim> claims)
    {
        ArgumentNullException.ThrowIfNull(claims, nameof(claims));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var accessToken = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.AccessExpireMinutes),
            signingCredentials: creds);

        var refreshToken = GenerateRefreshToken();
        return new JwtToken
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(accessToken),
            RefreshToken = refreshToken,
            AccessTokenExpiration = accessToken.ValidTo,
            RefreshTokenExpiration = DateTime.UtcNow.AddDays(_jwtSettings.RefreshExpireDays)
        };
    }

    private string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber).Replace("=", "").Replace("+", "-").Replace("/", "_");
    }
}
