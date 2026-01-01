using Microsoft.OpenApi.Models;

namespace ExampleClient.Extensions;

public static class SwaggerExtensions
{
    public static void AddCustomSwaggerGen(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Guardhouse Example API",
                Version = "v1",
                Description = "Example API demonstrating Guardhouse SDK with OAuth2/OpenID Connect"
            });

            options.CustomSchemaIds(type => type.FullName);

            var scheme = new OpenApiSecurityScheme
            {
                In = ParameterLocation.Header,
                Name = "Authorization",
                Flows = new OpenApiOAuthFlows
                {
                    AuthorizationCode = new OpenApiOAuthFlow
                    {
                        AuthorizationUrl = new Uri("https://your-guardhouse-server.com/connect/authorize"),
                        TokenUrl = new Uri("https://your-guardhouse-server.com/connect/token"),
                    }
                },
                Type = SecuritySchemeType.OAuth2,
                Scheme = "Bearer",
            };

            options.AddSecurityDefinition("OAuth", scheme);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Id = "OAuth",
                            Type = ReferenceType.SecurityScheme,
                        },
                    },
                    new List<string>()
                }
            });
        });
    }
}
