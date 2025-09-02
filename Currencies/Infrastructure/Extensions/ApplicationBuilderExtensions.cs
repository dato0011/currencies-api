namespace Currencies.Infrastructure.Extensions;

using Currencies.Infrastructure.Middlewares;

/// <summary>
/// Extension methods for configuring the application request pipeline.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Applies common middleware components such as exception handling, 
    /// HTTPS redirection, authentication/authorization, logging, and caching.
    /// </summary>
    public static IApplicationBuilder UseCustomMiddlewares(this IApplicationBuilder app)
    {
        app.UseExceptionMiddleware();
        app.UseHttpsRedirection();
        app.UseAccessTokenValidation();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRequestLoggingMiddleware();
        app.UseResponseCaching();

        return app;
    }
}
