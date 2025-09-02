using Currencies.Infrastructure;
using Currencies.Infrastructure.Extensions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Logging
builder.Services.AddCustomLogging(builder.Configuration);
builder.Host.UseSerilog();

// Http + Swagger
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient(Constants.HttpClientThirdPartyApi)
    .AddHttpMessageHandler<CorrelationHandler>();
builder.Services.AddCustomSwagger();

// Dependencies & Config
builder.Services.AddCustomAuthentication(builder.Configuration);
builder.Services.AddCustomDependencies(builder.Configuration);
builder.Services.AddCustomRateLimiting();
builder.Services.AddCustomVersioning();

// Controllers + Response Caching
builder.Services.AddControllers();
builder.Services.AddResponseCaching();

// OpenTelemetry
builder.Services.AddCustomTelemetry();

// API Explorer
builder.Services.AddOpenApi();

var app = builder.Build();
app.UseCustomMiddlewares();
app.MapControllers();

if (app.Environment.IsDevelopment())
{
    app.UseCustomSwagger();
}

try
{
    app.Run();
}
finally
{
    Log.CloseAndFlush();
}
