namespace Currencies.Infrastructure.Middlewares;

using Currencies.Repositories;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

public class AccessTokenValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IUserRepository _userRepository;

    public AccessTokenValidationMiddleware(RequestDelegate next, IUserRepository userRepository)
    {
        _next = next;
        _userRepository = userRepository;
    }

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

public static class AccessTokenValidationMiddlewareExtensions
{
    public static IApplicationBuilder UseAccessTokenValidation(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<AccessTokenValidationMiddleware>();
    }
}
