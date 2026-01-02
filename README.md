# Guardhouse SDK for .NET

Official .NET SDK for https://guardhouse.cloud platform. Use it for .NET clients when you already configured everything in Guardhouse Cloud.

## Installation

Install via NuGet:

```bash
dotnet add package Guardhouse.SDK
```

Or via NuGet Package Manager Console:

```
Install-Package Guardhouse.SDK
```

## Quick Start

### Client Application (Requesting Access Tokens)

Configure your application as a Client to request access tokens:

```csharp
using Guardhouse.SDK.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGuardhouseClient(options =>
{
    options.Authority = "https://your-guardhouse-server.com";
    options.ClientId = "your-client-id";
    options.ClientSecret = "your-client-secret";
    options.Scope = "api";
    options.EnableTokenCaching = true;
    options.EnableTokenRefresh = true;
});

var app = builder.Build();

// Example: Get access token and call protected API
app.MapGet("/api/call-protected", async (IGuardhouseTokenService tokenService) =>
{
    var accessToken = await tokenService.GetAccessTokenAsync();

    using var client = new HttpClient();
    client.DefaultRequestHeaders.Authorization = 
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

    var response = await client.GetAsync("https://protected-api.com/data");
    var data = await response.Content.ReadAsStringAsync();

    return Results.Ok(new
    {
        Response = data,
        AccessTokenPreview = accessToken.Substring(0, Math.Min(20, accessToken.Length)) + "..."
    });
});

app.Run();
```

Minimal configuration (no action delegate):

```csharp
builder.Services.AddGuardhouseClient(
    authority: "https://your-guardhouse-server.com",
    clientId: "your-client-id",
    clientSecret: "your-client-secret",
    scope: "api"
);
```

### Resource Server (Protecting APIs)

Configure your API as a Resource to validate incoming tokens:

```csharp
using Guardhouse.SDK.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGuardhouseResource(options =>
{
    options.Authority = "https://your-guardhouse-server.com";
    options.Audience = "my_resource_api";
    options.ValidationMode = TokenValidationMode.JwtSignature;
    options.ValidAlgorithms = new[] { "RS256" };
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Protected endpoint
app.MapGet("/api/protected", [Authorize] () =>
{
    return new { Message = "This is protected data", Timestamp = DateTime.UtcNow };
});

app.Run();
```

With Token Introspection (RFC 7662):

```csharp
builder.Services.AddGuardhouseResource(options =>
{
    options.Authority = "https://your-guardhouse-server.com";
    options.Audience = "my_resource_api";
    options.ValidationMode = TokenValidationMode.Introspection;
    options.IntrospectionClientId = "your-introspection-client-id";
    options.IntrospectionClientSecret = "your-introspection-client-secret";
});
```

## Configuration

### Client Options

```csharp
builder.Services.AddGuardhouseClient(options =>
{
    options.Authority = "https://your-guardhouse-server.com";       // Required
    options.ClientId = "your-client-id";                          // Required
    options.ClientSecret = "your-client-secret";                    // Required
    options.Scope = "api";                                        // Default: "api"
    
    // Optional features
    options.EnableTokenCaching = true;                              // Default: true
    options.CacheExpirationBufferSeconds = 60;                         // Default: 60
    options.EnableTokenRefresh = true;                                // Default: true
    
    // Resilience
    options.EnableHttpResilience = true;                             // Default: true
    options.RequestTimeoutSeconds = 30;                                // Default: 30
    options.MaxRetryAttempts = 3;                                     // Default: 3
});
```

### Resource Options

```csharp
builder.Services.AddGuardhouseResource(options =>
{
    options.Authority = "https://your-guardhouse-server.com";       // Required
    options.Audience = "my_resource_api";                        // Default: "my_resource_api"
    
    // Validation mode: JWT Signature or Introspection
    options.ValidationMode = TokenValidationMode.JwtSignature;    // Default: JWT Signature
    
    // Required for Introspection mode
    options.IntrospectionClientId = "your-client-id";          // Required for introspection
    options.IntrospectionClientSecret = "your-client-secret";    // Required for introspection
    
    // Token validation settings
    options.ValidateIssuer = true;                                 // Default: true
    options.ValidateAudience = true;                               // Default: true
    options.ValidateLifetime = true;                                // Default: true
    options.ValidateIssuerSigningKey = true;                        // Default: true
    
    // JWT validation
    options.ValidAlgorithms = new[] { "RS256" };                 // Default: RS256
    options.TokenTypes = new[] { "JWT" };                          // Default: JWT
    options.ClockSkew = TimeSpan.FromMinutes(5);               // Default: 5 minutes
    
    // JWKS caching
    options.JwksCacheDurationHours = 24;                           // Default: 24 hours
    options.JwksRefreshIntervalMinutes = 5;                        // Default: 5 minutes
    
    // HTTPS metadata
    options.RequireHttpsMetadata = true;                            // Default: true for HTTPS
});
```

## Features

### Token Management

✅ **Automatic Token Caching** - Tokens cached in memory with configurable expiration buffer

✅ **Automatic Token Refresh** - Seamless token renewal using refresh tokens

✅ **HTTP Resilience** - Built-in retry policies with exponential backoff

✅ **Introspection Support** - RFC 7662 token introspection for resource servers

### Security

✅ **Strict Algorithm Enforcement** - Only RS256 allowed (prevents algorithm confusion)

✅ **Token Type Validation** - Validates typ header to prevent type confusion

✅ **Comprehensive Validation** - Validates issuer, audience, lifetime, and signature

✅ **JWKS with Lazy Refresh** - Automatic refresh on unknown keys with rate limiting

✅ **Embedded Key Protection** - Prevents embedded key attacks

✅ **Key ID Injection Protection** - Prevents path traversal in kid injection

✅ **Psychic Signature Protection** - Library validates ECDSA signatures correctly

✅ **None Algorithm Prevention** - Rejects "none" algorithm tokens

### Developer Experience

✅ **Configuration Validation** - Fails fast at startup for missing required settings

✅ **Clear Error Messages** - Helpful messages for introspection credentials

✅ **Dependency Injection** - Full .NET DI support with extension methods

✅ **Logging Integration** - Built-in logging for debugging and monitoring

## API Reference

### Client Service

```csharp
public interface IGuardhouseTokenService
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
    Task<TokenResponse> RequestTokenAsync(CancellationToken cancellationToken = default);
    Task<TokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<IntrospectionResponse> IntrospectTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<bool> IsTokenActiveAsync(string token, CancellationToken cancellationToken = default);
}
```

### Resource Service

```csharp
public interface IGuardhouseResourceService
{
    Task<ClaimsPrincipal?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<IntrospectionResponse> IntrospectTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<AuthenticationScheme?> GetDefaultSchemeAsync(CancellationToken cancellationToken = default);
}
```

## Security Features

### Validation Modes

**JWT Signature Mode**:
- Uses JWKS endpoint for token validation
- Automatic key rotation support with lazy refresh
- Validates signature, issuer, audience, lifetime, algorithm, token type

**Introspection Mode** (RFC 7662):
- Validates tokens via Guardhouse introspection endpoint
- Client credentials required for introspection calls
- Caches introspection results until token expiration

### Protected Attack Vectors

1. ✅ Validates everything (signature, audience, issuer, lifetime, algorithm, type)
2. ✅ Strict RS256 enforcement prevents algorithm confusion
3. ✅ Issuer/audience validation prevents confused deputy attacks
4. ✅ JWKS library handles embedded key attacks
5. ✅ Valid algorithms list prevents algorithm confusion
6. ✅ "none" algorithm prevention
7. ✅ No fallback between validation methods
8. ✅ Token type (typ) validation prevents type confusion
9. ✅ Library prevents key ID injection (path traversal)
10. ✅ Library validates ECDSA/psychic signatures correctly

### JWKS Caching with Lazy Refresh

The SDK implements "Lazy Refresh on Unknown Key" strategy:

1. Extract `kid` (Key ID) from incoming JWT token header
2. Check local cache for the key
3. If key exists: Validate signature (fast path)
4. If key missing: Trigger JWKS refresh from `/.well-known/jwks.json`
5. Rate limit refresh (configurable, default 5 minutes)
6. Re-check cache and validate with new keys

This ensures your API accepts valid tokens even after key rotation, while protecting against DoS attacks via rate limiting.

## Examples

### Calling Protected APIs

```csharp
public class ExternalApiService
{
    private readonly IGuardhouseTokenService _tokenService;
    
    public ExternalApiService(IGuardhouseTokenService tokenService)
    {
        _tokenService = tokenService;
    }
    
    public async Task<string> CallProtectedEndpoint()
    {
        var accessToken = await _tokenService.GetAccessTokenAsync();
        
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        
        var response = await client.GetAsync("https://protected-api.com/data");
        return await response.Content.ReadAsStringAsync();
    }
}
```

### Protecting Endpoints

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    [HttpGet]
    public IActionResult GetAll()
    {
        return Ok(new[] { "Product 1", "Product 2" });
    }
    
    [HttpPost]
    [Authorize]
    public IActionResult Create([FromBody] CreateProductRequest request)
    {
        return CreatedAtAction(nameof(GetById), new { id = 1 }, request);
    }
    
    [HttpDelete("{id}")]
    [Authorize]
    public IActionResult Delete(int id)
    {
        return Ok(new { Message = $"Product {id} deleted" });
    }
}
```

### Authorization Policies

```csharp
builder.Services.AddAuthorization(options =>
{
    // Require specific scope
    options.AddPolicy("ReadAccess", policy =>
        policy.RequireClaim("scope", "read"));
    
    // Require admin role
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("admin"));
    
    // Require multiple conditions
    options.AddPolicy("CanManage", policy =>
        policy.RequireClaim("scope", "manage")
              .RequireRole("manager"));
});

[HttpGet("admin")]
[Authorize(Policy = "AdminOnly")]
public IActionResult AdminEndpoint()
{
    return Ok("Admin data");
}
```

## Dependencies

- .NET 8.0
- Microsoft.Extensions.DependencyInjection
- Microsoft.Extensions.Http
- Microsoft.Extensions.Http.Polly
- Microsoft.Extensions.Caching.Memory
- Microsoft.Extensions.Options
- Microsoft.AspNetCore.Authentication.JwtBearer
- System.IdentityModel.Tokens.Jwt
- System.Text.Json
- NodaTime
- Polly

## Troubleshooting

### Configuration Errors

**Error: "IntrospectionClientId is required when ValidationMode is set to Introspection"**

Add introspection credentials to your configuration:

```csharp
builder.Services.AddGuardhouseResource(options =>
{
    options.ValidationMode = TokenValidationMode.Introspection;
    options.IntrospectionClientId = "your-client-id";
    options.IntrospectionClientSecret = "your-client-secret";
});
```

### Token Validation Issues

**Error: "401 Unauthorized"**

1. Verify your Guardhouse credentials are correct
2. Check that Authority URL matches your Guardhouse instance
3. Ensure the client is enabled in Guardhouse Cloud
4. Verify token hasn't expired

### JWKS Refresh Issues

**Error: "kid not found" or signature validation fails**

1. Check your Authority URL is correct
2. Verify Guardhouse server is accessible
3. Check that `JwksRefreshIntervalMinutes` is appropriate (default 5)
4. Check application logs for JWKS refresh errors

## Documentation

- Guardhouse Documentation: https://docs.guardhouse.cloud
- GitHub Issues: https://github.com/guardhouse/guardhouse-sdk-dotnet/issues

## License

Licensed under the Apache License 2.0

## Support

- Guardhouse Cloud: https://guardhouse.cloud
- Documentation: https://docs.guardhouse.cloud
- GitHub Repository: https://github.com/guardhouse/guardhouse-sdk-dotnet
