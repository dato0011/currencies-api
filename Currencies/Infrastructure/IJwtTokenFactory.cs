namespace Currencies.Infrastructure;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.BearerToken;

/// <summary>
/// Factory interface for creating JWT tokens based on existing claims
/// </summary>
public interface IJwtTokenFactory
{
    /// <summary>
    /// Creates a JWT token with the provided claims
    /// </summary>
    /// <param name="claims">List of claims</param>
    /// <returns>An instance of <see cref="JwtSecurityToken"/></returns>
    JwtToken CreateToken(IEnumerable<Claim> claims);
}
