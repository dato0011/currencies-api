namespace Currencies.Infrastructure.Middlewares;

using Currencies.Repositories;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

/// <summary>
/// Middleware for validating JWT access tokens in incoming HTTP requests.
/// </summary>
public class AccessTokenValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IUserRepository _userRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccessTokenValidationMiddleware"/> class.
    /// </summary>
    /// <param name="next">The delegate to the next middleware in the pipeline.</param>
    /// <param name="userRepository">Repository for validating access tokens.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="next"/> or <paramref name="userRepository"/> is null.</exception>
    public AccessTokenValidationMiddleware(RequestDelegate next, IUserRepository userRepository)
    {
        _next = next;
        _userRepository = userRepository;
    }

    /// <summary>
    /// Validates the access token in the request's Authorization header and processes the request.
    /// </summary>
    /// <param name="context">The HTTP context of the current request.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// If the token is invalid or revoked, responds with a 401 Unauthorized status and an error message.
    /// Otherwise, passes the request to the next middleware in the pipeline.
    /// </remarks>
    public async Task InvokeAsync(HttpContext context)
    {
        var token = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
        if (!string.IsNullOrEmpty(token) && await _userRepository.ValidateAccessTokenAsync(token) is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Invalid or revoked access token");
            return;
        }

        await _next(context);
    }
}

/// <summary>
/// Extension methods for registering the <see cref="AccessTokenValidationMiddleware"/> in the application pipeline.
/// </summary>
public static class AccessTokenValidationMiddlewareExtensions
{
    /// <summary>
    /// Adds the <see cref="AccessTokenValidationMiddleware"/> to the application pipeline.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The updated application builder.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>    
    public static IApplicationBuilder UseAccessTokenValidation(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<AccessTokenValidationMiddleware>();
    }
}
