# Guardhouse SDK for .NET

Guardhouse SDK for .NET provides a **complete replacement for OpenIdDict** in .NET applications. This SDK enables your .NET applications to integrate with Guardhouse OpenID Connect/OAuth IDaaS Identity Server for token management and API protection.

## What is Guardhouse?

Guardhouse is an OpenID/OAuth IDaaS Identity Server that provides:
- OAuth 2.0 token endpoint
- Token introspection endpoint
- JWT token validation
- User authentication and authorization
- OpenID Connect discovery

**Note:** This SDK is a **modern replacement** for OpenIdDict. You can replace OpenIdDict dependencies with Guardhouse SDK for simplified token management and authentication.

## Understanding Client vs Resource

### Client
A **Client** is an application that requests access tokens from Guardhouse to call protected services. Examples:
- Web applications calling backend APIs
- Microservices calling other services
- Console applications or background workers

### Resource  
A **Resource** is an API service that validates incoming tokens and protects endpoints. Examples:
- Web APIs with `[Authorize]` attributes
- Microservice endpoints
- Any service that needs to validate JWT tokens

## Installation

Install the NuGet package:

```bash
dotnet add package Guardhouse.SDK
```

Or via NuGet Package Manager:

```
Install-Package Guardhouse.SDK
```

## Quick Start

### As a Client (Token Management)

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add Guardhouse client services
builder.Services.AddGuardhouseClient(options =>
{
    options.Authority = "https://your-guardhouse-server.com";
    options.ClientId = "your-client-id";
    options.ClientSecret = "your-client-secret";
    options.Scope = "api";
    options.EnableTokenCaching = true; // Optional: cache tokens in memory
    options.EnableTokenRefresh = true;  // Optional: auto-refresh tokens
    options.EnableHttpResilience = true; // Optional: retry and timeout
});

var app = builder.Build();

// Example: Using the token service
app.MapGet("/api/data", async (IGuardhouseTokenService tokenService) =>
{
    var accessToken = await tokenService.GetAccessTokenAsync();
    
    // Use the access token to call protected APIs
    using var client = new HttpClient();
    client.DefaultRequestHeaders.Authorization = 
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
    
    var response = await client.GetAsync("https://protected-api.com/data");
    return await response.Content.ReadAsStringAsync();
});

app.Run();
```

### As a Resource (API Protection)

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add Guardhouse resource server services
builder.Services.AddGuardhouseResource(options =>
{
    options.Authority = "https://your-guardhouse-server.com";
    options.Audience = "your-api-audience";
    options.EnableIntrospection = false; // Optional: enable token introspection
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Protected endpoint
app.MapGet("/api/protected-data", [Authorize] () =>
{
    return new { Message = "This is protected data", Timestamp = DateTime.UtcNow };
});

app.Run();
```

## Advanced Configuration

### Authorization with Claims and Policies

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add Guardhouse resource server
builder.Services.AddGuardhouseResource(options =>
{
    options.Authority = "https://your-guardhouse-server.com";
    options.Audience = "your-api-audience";
});

// Configure authorization policies
builder.Services.AddAuthorization(options =>
{
    // Policy for users with 'admin' scope
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireClaim("scope", "admin"));
    
    // Policy for users with specific role
    options.AddPolicy("ManagerRole", policy =>
        policy.RequireClaim("role", "manager"));
    
    // Policy for users with multiple requirements
    options.AddPolicy("SeniorAdmin", policy =>
        policy.RequireClaim("scope", "admin")
              .RequireClaim("role", "senior_admin")
              .RequireClaim("department", "IT"));
    
    // Custom policy with complex logic
    options.AddPolicy("CanAccessSensitiveData", policy =>
        policy.RequireAssertion(context =>
        {
            var user = context.User;
            var hasAdminScope = user.HasClaim("scope", "admin");
            var hasManagerRole = user.HasClaim("role", "manager");
            var department = user.FindFirst("department")?.Value;
            
            return hasAdminScope || (hasManagerRole && department == "Finance");
        }));
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Endpoints with different authorization requirements
app.MapGet("/api/admin", [Authorize(Policy = "AdminOnly")] () =>
{
    return new { Message = "Admin only endpoint" };
});

app.MapGet("/api/manager", [Authorize(Policy = "ManagerRole")] () =>
{
    return new { Message = "Manager only endpoint" };
});

app.MapGet("/api/sensitive", [Authorize(Policy = "CanAccessSensitiveData")] () =>
{
    return new { Message = "Sensitive data endpoint" });
});

app.Run();
```

### Custom Authorization Handlers

```csharp
// Custom requirement class
public class MinimumAgeRequirement : IAuthorizationRequirement
{
    public int MinimumAge { get; }
    public MinimumAgeRequirement(int minimumAge) => MinimumAge = minimumAge;
}

// Custom handler
public class MinimumAgeHandler : AuthorizationHandler<MinimumAgeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MinimumAgeRequirement requirement)
    {
        var dateOfBirthClaim = context.User.FindFirst(c => c.Type == "date_of_birth");
        if (dateOfBirthClaim == null)
        {
            return Task.CompletedTask;
        }

        var dateOfBirth = Convert.ToDateTime(dateOfBirthClaim.Value);
        int calculatedAge = DateTime.Today.Year - dateOfBirth.Year;
        if (dateOfBirth > DateTime.Today.AddYears(-calculatedAge))
        {
            calculatedAge--;
        }

        if (calculatedAge >= requirement.MinimumAge)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

// Register in Program.cs
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AtLeast21", policy =>
        policy.Requirements.Add(new MinimumAgeRequirement(21)));
});

builder.Services.AddSingleton<IAuthorizationHandler, MinimumAgeHandler>();
```

### Resource-Based Authorization

```csharp
// Resource-based requirement
public class SameUserRequirement : IAuthorizationRequirement
{
    public string UserId { get; }
    public SameUserRequirement(string userId) => UserId = userId;
}

// Handler for same-user access
public class SameUserHandler : AuthorizationHandler<SameUserRequirement, string>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SameUserRequirement requirement,
        string resourceUserId)
    {
        var currentUserId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (currentUserId == resourceUserId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

// Usage in controller
[HttpGet("profile/{userId}")]
[Authorize]
public async Task<IActionResult> GetUserProfile(string userId)
{
    var result = await _authorizationService.AuthorizeAsync(
        User, userId, new SameUserRequirement(userId));
    
    if (!result.Succeeded)
    {
        return Forbid();
    }

    var profile = await _userService.GetProfileAsync(userId);
    return Ok(profile);
}
```

## OpenIdDict Integration

The Guardhouse SDK fully covers OpenIdDict scenarios. Here's how to integrate with OpenIdDict validation:

### OpenIdDict Validation Setup

```csharp
// Program.cs - OpenIdDict validation with Guardhouse SDK
var builder = WebApplication.CreateBuilder(args);

// Configure OpenIdDict validation (similar to your setup)
services.AddOpenIddict()
    .AddValidation(options =>
    {
        options.SetIssuer(identityOptions.Issuer);
        options.AddAudiences(identityOptions.Audiences);

        options.UseIntrospection()
            .SetClientId(identityOptions.ClientId)
            .SetClientSecret(identityOptions.ClientSecret);

        options.AddEventHandler<OpenIddictValidationEvents.ProcessErrorContext>(builder =>
        {
            builder.UseInlineHandler(notification =>
            {
                if (notification.CancellationToken.IsCancellationRequested)
                {
                    notification.HandleRequest();
                }

                return ValueTask.CompletedTask;
            });
        });

        options.UseSystemNetHttp()
            .SetHttpResiliencePipeline(pipeline =>
            {
                pipeline.AddTimeout(TimeSpan.FromSeconds(30));
                pipeline.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    BackoffType = DelayBackoffType.Exponential,
                    Delay = TimeSpan.FromSeconds(2),
                    MaxDelay = TimeSpan.FromSeconds(30),
                    UseJitter = true,
                    ShouldHandle = args =>
                        ValueTask.FromResult(HttpClientResiliencePredicates.IsTransient(args.Outcome)),
                });
            });

        options.UseAspNetCore();
    });

// Add Guardhouse SDK for additional features
services.AddGuardhouseResource(options =>
{
    options.Authority = identityOptions.Issuer;
    options.Audience = identityOptions.Audiences.FirstOrDefault();
    options.EnableIntrospection = true;
    options.IntrospectionClientId = identityOptions.ClientId;
    options.IntrospectionClientSecret = identityOptions.ClientSecret;
});

services.AddAuthentication(options =>
{
    options.DefaultScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
});

// Add authorization with policies
services.AddAuthorization(options =>
{
    options.AddPolicy("ApiScope", policy =>
        policy.RequireClaim("scope", "api"));
    
    options.AddPolicy("AdminScope", policy =>
        policy.RequireClaim("scope", "admin"));
});
```

### Enhanced Error Handling with Cancellation

```csharp
// Custom middleware for better cancellation handling
public class GuardhouseCancellationMiddleware
{
    private readonly RequestDelegate _next;

    public GuardhouseCancellationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            if (context.RequestAborted.IsCancellationRequested)
            {
                context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
            }
            return Task.CompletedTask;
        });

        await _next(context);
    }
}

// Register in Program.cs
app.UseMiddleware<GuardhouseCancellationMiddleware>();
```

## Configuration Options

### Client Configuration

```csharp
builder.Services.AddGuardhouseClient(options =>
{
    options.Authority = "https://your-guardhouse-server.com"; // Required
    options.ClientId = "your-client-id";                      // Required
    options.ClientSecret = "your-client-secret";              // Required
    options.Scope = "api";                                    // Default: "api"
    
    // Optional features
    options.EnableTokenCaching = true;                        // Default: true
    options.CacheExpirationBufferSeconds = 60;               // Default: 60
    options.EnableTokenRefresh = true;                        // Default: true
    
    // Resilience options
    options.EnableHttpResilience = true;                      // Default: true
    options.RequestTimeoutSeconds = 30;                       // Default: 30
    options.MaxRetryAttempts = 3;                             // Default: 3
});
```

### Resource Configuration

```csharp
builder.Services.AddGuardhouseResource(options =>
{
    options.Authority = "https://your-guardhouse-server.com"; // Required
    options.Audience = "your-api-audience";                   // Optional
    
    // Optional features
    options.EnableIntrospection = false;                      // Default: false
    options.IntrospectionClientId = "introspection-client";  // Required if introspection enabled
    options.IntrospectionClientSecret = "introspection-secret"; // Required if introspection enabled
    options.PolicyName = "Guardhouse";                        // Default: "Guardhouse"
    
    // Token validation options
    options.ValidateIssuer = true;                            // Default: true
    options.ValidateAudience = true;                          // Default: true if Audience specified
    options.ValidateLifetime = true;                          // Default: true
    options.ClockSkew = TimeSpan.FromMinutes(5);              // Default: 5 minutes
    options.RequireHttpsMetadata = true;                     // Default: true for HTTPS
});
```

## Advanced Usage

### Minimal Configuration

For quick setup, use the minimal configuration methods:

```csharp
// Client with minimal config
builder.Services.AddGuardhouseClient(
    authority: "https://your-guardhouse-server.com",
    clientId: "your-client-id",
    clientSecret: "your-client-secret",
    scope: "api"
);

// Resource with minimal config
builder.Services.AddGuardhouseResource(
    authority: "https://your-guardhouse-server.com",
    audience: "your-api-audience"
);
```

### Both Client and Resource

If your application needs to both request tokens AND protect endpoints:

```csharp
builder.Services.AddGuardhouse(
    configureClientAction: clientOptions =>
    {
        clientOptions.Authority = "https://your-guardhouse-server.com";
        clientOptions.ClientId = "your-client-id";
        clientOptions.ClientSecret = "your-client-secret";
    },
    configureResourceAction: resourceOptions =>
    {
        resourceOptions.Authority = "https://your-guardhouse-server.com";
        resourceOptions.Audience = "your-api-audience";
    }
);
```

### Manual Token Operations with NodaTime

```csharp
public class MyService
{
    private readonly IGuardhouseTokenService _tokenService;

    public MyService(IGuardhouseTokenService tokenService)
    {
        _tokenService = tokenService;
    }

    public async Task<string> GetValidToken()
    {
        // Automatically handles caching and refresh
        return await _tokenService.GetAccessTokenAsync();
    }

    public async Task<TokenResponse> RequestNewToken()
    {
        // Force request a new token
        return await _tokenService.RequestTokenAsync();
    }

    public async Task<TokenResponse> RefreshExistingToken(string refreshToken)
    {
        // Refresh using a refresh token
        return await _tokenService.RefreshTokenAsync(refreshToken);
    }

    public async Task<bool> ValidateToken(string token)
    {
        // Check if token is active using introspection
        return await _tokenService.IsTokenActiveAsync(token);
    }

    public void AnalyzeTokenExpiration(TokenResponse token)
    {
        // Use NodaTime for precise time calculations
        var now = SystemClock.Instance.GetCurrentInstant();
        var timeUntilExpiry = token.ExpiresAt - now;
        
        Console.WriteLine($"Token expires in: {timeUntilExpiry}");
        Console.WriteLine($"Token is expired: {token.IsExpired()}");
    }
}
```

### Custom Authentication Policies with Claims

```csharp
// Program.cs
builder.Services.AddAuthorization(options =>
{
    // Scope-based policies
    options.AddPolicy("ReadAccess", policy =>
        policy.RequireClaim("scope", "read", "api"));
    
    options.AddPolicy("WriteAccess", policy =>
        policy.RequireClaim("scope", "write", "admin"));
    
    // Role-based policies
    options.AddPolicy("Users", policy =>
        policy.RequireRole("user"));
    
    options.AddPolicy("Administrators", policy =>
        policy.RequireRole("admin", "super_admin"));
    
    // Combined policies
    options.AddPolicy("CanManageUsers", policy =>
        policy.RequireClaim("scope", "user_management")
              .RequireRole("admin", "hr"));
    
    // Department-based policies
    options.AddPolicy("FinanceAccess", policy =>
        policy.RequireClaim("department", "finance")
              .RequireClaim("access_level", "confidential"));
    
    // Custom assertion policies
    options.AddPolicy("CanAccessOwnData", policy =>
        policy.RequireAssertion(context =>
        {
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var requestedUserId = context.Resource as string;
            return userId == requestedUserId;
        }));
});

// Controller examples
[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "ReadAccess")]
    public async Task<IActionResult> GetDocuments()
    {
        // User must have 'read' or 'api' scope
        return Ok(await _documentService.GetAllAsync());
    }

    [HttpPost]
    [Authorize(Policy = "WriteAccess")]
    public async Task<IActionResult> CreateDocument([FromBody] CreateDocumentRequest request)
    {
        // User must have 'write' or 'admin' scope
        var document = await _documentService.CreateAsync(request);
        return CreatedAtAction(nameof(GetDocument), new { id = document.Id }, document);
    }

    [HttpGet("{userId}/documents")]
    [Authorize(Policy = "CanAccessOwnData")]
    public async Task<IActionResult> GetUserDocuments(string userId)
    {
        // User can only access their own documents
        var documents = await _documentService.GetUserDocumentsAsync(userId);
        return Ok(documents);
    }

    [HttpGet("financial")]
    [Authorize(Policy = "FinanceAccess")]
    public async Task<IActionResult> GetFinancialReports()
    {
        // User must be in finance department with confidential access
        return Ok(await _reportService.GetFinancialReportsAsync());
    }
}
```

## Features

### ✅ NodaTime Integration
- Uses NodaTime `Instant` for precise time handling
- Better timezone support and time calculations
- Backward compatibility with `DateTime` properties

### ✅ Token Caching
- Automatic in-memory caching of access tokens
- Configurable expiration buffer to prevent using expired tokens
- Optional feature (can be disabled)

### ✅ Token Refresh
- Automatic token refresh using refresh tokens
- Seamless token management without manual intervention
- Optional feature (can be disabled)

### ✅ Token Introspection
- Validate tokens using Guardhouse introspection endpoint
- Additional security layer for resource servers
- Optional feature (can be disabled)

### ✅ HTTP Resilience
- Built-in retry policies with exponential backoff
- Configurable timeouts and retry attempts
- Polly-based resilience pipeline
- Cancellation token support throughout

### ✅ Easy Integration
- Simple extension methods for Program.cs setup
- Minimal configuration options
- Full support for .NET dependency injection

### ✅ JWT Bearer Authentication
- Built-in JWT token validation
- Configurable token validation parameters
- Support for custom authentication events
- Cancellation token handling

### ✅ OpenIdDict Compatibility
- Full coverage of OpenIdDict validation scenarios
- Compatible with OpenIdDict resilience patterns
- Supports introspection and error handling
- Works alongside OpenIdDict event handlers

## Dependencies

The SDK depends on the following NuGet packages:
- `Microsoft.Extensions.DependencyInjection`
- `Microsoft.Extensions.Http`
- `Microsoft.Extensions.Http.Polly`
- `Microsoft.Extensions.Caching.Memory`
- `Microsoft.Extensions.Options`
- `Microsoft.AspNetCore.Authentication.JwtBearer`
- `System.IdentityModel.Tokens.Jwt`
- `System.Text.Json`
- `NodaTime`
- `Polly`
- `Polly.Extensions`

## .NET Version Support

- .NET 8.0 and later
- Compatible with .NET Core 3.1+ (with some modifications)

## License

This SDK is licensed under the MIT License.

## Support

For issues and questions:
- GitHub Issues: [https://github.com/guardhouse/guardhouse-sdk-dotnet/issues](https://github.com/guardhouse/guardhouse-sdk-dotnet/issues)
- Documentation: [https://docs.guardhouse.com](https://docs.guardhouse.com)

## Examples Repository

For complete working examples, check out our examples repository:
[https://github.com/guardhouse/guardhouse-sdk-dotnet-examples](https://github.com/guardhouse/guardhouse-sdk-dotnet-examples)

## 🚀 Deployment Guide

See [DEPLOYMENT.md](DEPLOYMENT.md) for comprehensive deployment instructions including:
- Local development setup
- CI/CD pipeline with GitHub Actions
- Manual publishing to NuGet.org
- Versioning strategy and release management
- Troubleshooting common issues
- Monitoring and quality gates