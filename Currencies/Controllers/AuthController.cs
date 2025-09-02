namespace Currency.Controllers;

using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using Currencies.Entities;
using Currencies.Models;
using Currencies.Infrastructure;
using Currencies.Repositories;
using Microsoft.AspNetCore.RateLimiting;

/// <summary>
/// Controller responsible for handling user authentication and JWT token management, including login and token refresh operations.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[EnableRateLimiting("GlobalPolicy")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtTokenFactory _jwtTokenFactory;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthController"/> class.
    /// </summary>
    /// <param name="userRepository">Repository for accessing and managing user data.</param>
    /// <param name="jwtTokenFactory">Factory for creating JWT access and refresh tokens.</param>
    /// <param name="logger">Logger for recording authentication-related events.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public AuthController(IUserRepository userRepository, IJwtTokenFactory jwtTokenFactory, ILogger logger)
    {
        _userRepository = userRepository;
        _jwtTokenFactory = jwtTokenFactory;
        _logger = logger;
    }

    /// <summary>
    /// Authenticates a user and issues JWT access and refresh tokens upon successful login.
    /// </summary>
    /// <param name="model">The login request containing username and password.</param>
    /// <returns>An <see cref="ActionResult{T}"/> containing a <see cref="JwtTokenResponseModel"/> with access and refresh tokens, or an error response.</returns>
    /// <response code="200">Returns the JWT access and refresh tokens upon successful authentication.</response>
    /// <response code="401">Returned if the username or password is invalid or missing.</response>
    [HttpPost("login")]
    public async Task<ActionResult<JwtTokenResponseModel>> Login([FromBody] LoginRequestModel model)
    {
        // In prod: Validate against DB/auth provider
        if (string.IsNullOrWhiteSpace(model.Username) || string.IsNullOrWhiteSpace(model.Password)) return Unauthorized();

        // TODO: Thou shall hash the password
        User? user = _userRepository.GetUser(model.Username, model.Password);
        if (user == null)
        {
            _logger.Debug("Invalid login attempt for user {Username}", model.Username);
            return Unauthorized();
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Role, user.Role)
        };

        _logger.Debug("{Username} authenticated successfully. Generating tokens...", model.Username);

        JwtToken token = _jwtTokenFactory.CreateToken(claims);

        _logger.Debug("Storing tokens for user {Username} in the repository", model.Username);

        await _userRepository.StoreAccessTokenAsync(user.Username, token.AccessToken, token.AccessTokenExpiration);
        await _userRepository.StoreRefreshTokenAsync(user.Username, token.RefreshToken, token.RefreshTokenExpiration);

        _logger.Information("User {Username} logged in successfully", model.Username);

        return Ok(new JwtTokenResponseModel( // Use Mapster/AutoMapper in real projects
            token.AccessToken,
            token.RefreshToken,
            token.AccessTokenExpiration,
            token.RefreshTokenExpiration
        ));
    }

    /// <summary>
    /// Refreshes JWT access and refresh tokens using a valid refresh token, revoking the old tokens.
    /// </summary>
    /// <param name="model">The refresh token request containing the refresh token.</param>
    /// <returns>An <see cref="ActionResult{T}"/> containing a new <see cref="JwtTokenResponseModel"/> with updated tokens, or an error response.</returns>
    /// <response code="200">Returns new JWT access and refresh tokens upon successful refresh.</response>
    /// <response code="400">Returned if the refresh token is missing or invalid.</response>
    /// <response code="401">Returned if the refresh token is invalid or expired.</response>
    [HttpPost("refresh")]
    public async Task<ActionResult<JwtTokenResponseModel>> Refresh([FromBody] RefreshTokenRequestModel model)
    {
        if (string.IsNullOrWhiteSpace(model.RefreshToken))
        {
            _logger.Debug("Refresh token request failed: Invalid refresh token");
            return BadRequest(new { Message = "RefreshToken is required" });
        }

        User? user = await _userRepository.ValidateRefreshTokenAsync(model.RefreshToken);
        if (user == null)
        {
            _logger.Debug("Refresh token request failed: Invalid or expired refresh token");
            return Unauthorized(new { Message = "Invalid or expired refresh token" });
        }

        JwtToken token = _jwtTokenFactory.CreateToken(User.Claims);

        _logger.Debug("Storing new tokens for user {Username} in the repository", user.Username);

        await _userRepository.StoreAccessTokenAsync(user.Username, token.AccessToken, token.AccessTokenExpiration);
        await _userRepository.StoreRefreshTokenAsync(user.Username, token.RefreshToken, token.RefreshTokenExpiration);

        var oldToken = HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");;

        _logger.Debug("Revoking old token for user {Username}", user.Username);
        await _userRepository.RevokeRefreshTokenAsync(model.RefreshToken); // Rotate refresh token as well
        // TODO: All access tokens linked to this refresh token should be revoked as well in a real-world scenario.
        // That would require linking access/refresh tokens in Redis. Not implemented here for brevity.
        // Or just use Cognito/IdentityServer.

        _logger.Information("Refresh token for user {Username} processed successfully", user.Username);

        return Ok(new JwtTokenResponseModel( // Use Mapster/AutoMapper in real projects
            token.AccessToken,
            token.RefreshToken,
            token.AccessTokenExpiration,
            token.RefreshTokenExpiration
        ));
    }    
}
