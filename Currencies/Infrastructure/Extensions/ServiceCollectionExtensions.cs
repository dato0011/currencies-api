namespace Currencies.Infrastructure.Extensions;

using Currencies.Infrastructure;
using Currencies.Infrastructure.Configuration;
using Currencies.Infrastructure.Implementations;
using Currencies.Repositories;
using Currencies.Repositories.Implementations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

/// <summary>
/// Extension methods for registering application services and infrastructure components.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configures Serilog logging and registers the logger as a singleton service.
    /// </summary>
    public static IServiceCollection AddCustomLogging(this IServiceCollection services, IConfiguration configuration)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .CreateLogger();

        services.AddSingleton(Log.Logger);
        return services;
    }

    /// <summary>
    /// Configures JWT authentication using settings from configuration.
    /// </summary>
    public static IServiceCollection AddCustomAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        var jwtSettings = configuration.GetSection("Jwt").Get<JwtSettings>()
            ?? throw new InvalidOperationException("JWT settings are missing");

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key))
            };
            options.RequireHttpsMetadata = true;
            options.SaveToken = true;
        });

        return services;
    }

    /// <summary>
    /// Registers core application dependencies, including repositories, providers, and Redis cache.
    /// </summary>
    public static IServiceCollection AddCustomDependencies(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FrankfurterApiConfig>(configuration.GetSection("FrankfurterApi"));

        services.AddSingleton<HashAlgorithm>(_ => SHA256.Create());
        services.AddSingleton<IJwtTokenFactory, JwtTokenFactory>();
        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<ISimpleCacheProvider, SimpleCacheProvider>();
        services.AddSingleton<IUnsupportedSymbolsHandler, UnsupportedSymbolsHandler>();
        services.AddSingleton<ICurrencyRateProviderFactory, CurrencyRateProviderFactory>();
        services.AddSingleton<CorrelationHandler>();
        services.AddKeyedSingleton<IApiResiliencePolicies, ApiResiliencePolicies>(Constants.DiKeyFraknfurterResiliencePolicy);
        services.AddKeyedSingleton<ICurrencyRateProvider, FrankfurterRateProvider>(Constants.ProviderFrankfurter);

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration.GetConnectionString("Redis");
            options.InstanceName = "Currencies_";
        });

        return services;
    }

    /// <summary>
    /// Configures rate limiting policies for authenticated and global requests.
    /// </summary>
    public static IServiceCollection AddCustomRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter("AuthenticatedUserPolicy", opt =>
            {
                opt.PermitLimit = 100;
                opt.Window = TimeSpan.FromMinutes(1);
                opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opt.QueueLimit = 10;
                opt.AutoReplenishment = true;
            });

            options.AddFixedWindowLimiter("GlobalPolicy", opt =>
            {
                opt.PermitLimit = 30;
                opt.Window = TimeSpan.FromMinutes(1);
                opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opt.QueueLimit = 5;
            });
        });

        return services;
    }

    /// <summary>
    /// Configures API versioning with URL segment reader and default version settings.
    /// </summary>
    public static IServiceCollection AddCustomVersioning(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        });

        return services;
    }

    /// <summary>
    /// Configures OpenTelemetry tracing with AspNetCore and HttpClient instrumentation.
    /// </summary>
    public static IServiceCollection AddCustomTelemetry(this IServiceCollection services)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("CurrenciesApi"))
            .WithTracing(tracerProviderBuilder =>
            {
                tracerProviderBuilder
                    .AddSource("CurrenciesApi")
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService("CurrenciesApi", serviceVersion: "1.0.0"))
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter();
            });

        return services;
    }
}
