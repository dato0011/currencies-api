namespace Currencies.Infrastructure.Extensions;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using System.Reflection;

/// <summary>
/// Provides extension methods for configuring Swagger in the API.
/// </summary>
public static class SwaggerExtensions
{
    /// <summary>
    /// Adds and configures Swagger generation for the API, including XML comments and JWT authentication support.
    /// </summary>
    /// <param name="services">The IServiceCollection to add Swagger services to.</param>
    /// <returns>The updated IServiceCollection.</returns>
    public static IServiceCollection AddCustomSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Currencies API",
                Version = "v1",
                Description = "API to for handling currency-related API requests, including retrieving latest and historical exchange rates.",
                Contact = new OpenApiContact
                {
                    Name = "David Popiashvili",
                    Email = "dpopiashvili@outlook.com"
                }
            });

            // Include XML comments if available
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                c.IncludeXmlComments(xmlPath);
            }

            // Optional: Add JWT authentication support
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                In = ParameterLocation.Header,
                Description = "Please enter JWT with Bearer into field (e.g., 'Bearer {token}')",
                Name = "Authorization",
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    new string[] {}
                }
            });
        });

        return services;
    }

    /// <summary>
    /// Registers Swagger middleware and configures it's route.
    /// </summary>
    /// <param name="app"><see cref="IApplicationBuilder"/> instance</param>
    /// <returns><see cref="IApplicationBuilder"/> instance</returns>
    public static IApplicationBuilder UseCustomSwagger(this IApplicationBuilder app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Currencies API V1");
            c.RoutePrefix = "swagger";
            c.DisplayRequestDuration();
        });

        return app;
    }
}
