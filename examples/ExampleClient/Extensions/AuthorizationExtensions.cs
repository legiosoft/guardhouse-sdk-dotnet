using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace ExampleClient.Extensions;

public static class AuthorizationExtensions
{
    public static void AddCustomAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy("ReadAccess", policy =>
                policy.RequireClaim("scope", "read", "api"));
            
            options.AddPolicy("WriteAccess", policy =>
                policy.RequireClaim("scope", "write", "admin"));
            
            options.AddPolicy("AdminOnly", policy =>
                policy.RequireClaim("scope", "admin"));
        });
    }
}
