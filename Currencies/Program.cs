using Currencies.Infrastructure;
using Currencies.Infrastructure.Configuration;
using Currencies.Infrastructure.Implementations;
using Currencies.Infrastructure.Middlewares;
using Currencies.Repositories;
using Currencies.Repositories.Implementations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;
using System.Security.Cryptography;
using System.Threading.RateLimiting;
using System.Text;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();
builder.Host.UseSerilog();

Serilog.Debugging.SelfLog.Enable(msg => Debug.WriteLine(msg));


builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient(Constants.HttpClientThirdPartyApi)
    .AddHttpMessageHandler<CorrelationHandler>();

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<FrankfurterApiConfig>(builder.Configuration.GetSection("FrankfurterApi"));

builder.Services.AddSingleton<HashAlgorithm>(_ => SHA256.Create());
builder.Services.AddSingleton<IJwtTokenFactory, JwtTokenFactory>();
builder.Services.AddSingleton<IUserRepository, UserRepository>();
builder.Services.AddSingleton<ISimpleCacheProvider, SimpleCacheProvider>();
builder.Services.AddSingleton<IUnsupportedSymbolsHandler, UnsupportedSymbolsHandler>();
builder.Services.AddSingleton<ICurrencyRateProviderFactory, CurrencyRateProviderFactory>();
builder.Services.AddSingleton<CorrelationHandler>();
builder.Services.AddSingleton(Log.Logger);
builder.Services.AddKeyedSingleton<IApiResiliencePolicies, ApiResiliencePolicies>(Constants.DiKeyFraknfurterResiliencePolicy);
builder.Services.AddKeyedSingleton<ICurrencyRateProvider, FrankfurterRateProvider>(Constants.ProviderFrankfurter);

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "Currencies_";
});

builder.Services.AddRateLimiter(options =>
{
    // These values should be configurable in real project.
    // Rate limiter should also be configured in api gateway.

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

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
        ?? throw new InvalidOperationException("JWT settings are missing");
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

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});
builder.Services.AddResponseCaching();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName: "CurrenciesApi"))
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .AddSource("CurrenciesApi") 
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService(serviceName: "CurrenciesApi", serviceVersion: "1.0.0"))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter();
        // Wasn't able to make Redis instrumentation work since I rely on Microsoft.Extensions.Caching.StackExchangeRedis
        // while OT Redis provider needs StackExchange.Redis. 
        //.AddRedisInstrumentation(redisMultiplexer) 

    });

var app = builder.Build();
app.UseExceptionMiddleware();

app.UseHttpsRedirection();
app.UseAccessTokenValidation();
app.UseAuthentication();
app.UseAuthorization();
app.UseRequestLoggingMiddleware();
app.UseResponseCaching();
app.MapControllers();

try
{
    app.Run();
}
finally
{
    Log.CloseAndFlush();
}
