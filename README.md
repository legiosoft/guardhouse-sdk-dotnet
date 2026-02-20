# Guardhouse SDK for .NET

Official .NET SDK for Guardhouse Cloud. Use it in .NET applications to request access tokens or validate incoming tokens against your Guardhouse instance.

## Installation

Install via NuGet:

```bash
dotnet add package Guardhouse.SDK
```

Or via NuGet Package Manager Console:

```
Install-Package Guardhouse.SDK
```

**Supported Frameworks**: .NET 6.0, 7.0, 8.0, 9.0, 10.0

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
    options.IncludeOfflineAccessScope = true;
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

Minimal configuration:

```csharp
builder.Services.AddGuardhouseClient(
    authority: "https://your-guardhouse-server.com",
    clientId: "your-client-id",
    clientSecret: "your-client-secret",
    scope: "api offline_access"
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

If your identity server does not accept Basic Authentication for introspection, use FormData credential transmission:

```csharp
builder.Services.AddGuardhouseResource(options =>
{
    options.Authority = "https://your-guardhouse-server.com";
    options.Audience = "my_resource_api";
    options.ValidationMode = TokenValidationMode.Introspection;
    options.IntrospectionClientId = "your-introspection-client-id";
    options.IntrospectionClientSecret = "your-introspection-client-secret";
    options.IntrospectionCredentialTransmission = IntrospectionCredentialTransmission.FormData;
});
```

## Configuration

### Client Options

```csharp
builder.Services.AddGuardhouseClient(options =>
{
    options.Authority = "https://your-guardhouse-server.com";            // Required
    options.RequireHttps = true;                                          // Default: true
    options.ClientId = "your-client-id";                                 // Required
    options.ClientSecret = "your-client-secret";                         // Required
    options.Scope = "api";                                                // Default: "api"

    // Token caching & refresh
    options.EnableTokenCaching = true;                                   // Default: true
    options.CacheExpirationBufferSeconds = 60;                           // Default: 60
    options.RefreshTokenCacheDurationDays = 30;                          // Default: 30
    options.EnableTokenRefresh = true;                                   // Default: true
    options.IncludeOfflineAccessScope = false;                           // Default: false (set true if your identity server requires offline_access for refresh tokens)

    // Resilience
    options.EnableHttpResilience = true;                                 // Default: true
    options.RequestTimeoutSeconds = 30;                                  // Default: 30
    options.MaxRetryAttempts = 3;                                        // Default: 3

    // Introspection (optional - for debugging purposes)
    options.IntrospectionClientId = "your-introspection-client-id";      // Optional
    options.IntrospectionClientSecret = "your-introspection-client-secret"; // Optional
    options.IntrospectionCredentialTransmission = IntrospectionCredentialTransmission.BasicAuth; // Default: BasicAuth

    options.EnableDebug = false;                                         // Default: false
});
```

### Resource Options

```csharp
builder.Services.AddGuardhouseResource(options =>
{
    options.Authority = "https://your-guardhouse-server.com";            // Required
    options.Audience = "my_resource_api";                                // Required
    options.PolicyName = "Guardhouse";                                    // Default: "Guardhouse"

    // Validation mode: JWT Signature or Introspection
    options.ValidationMode = TokenValidationMode.JwtSignature;            // Default: JWT Signature

    // Required for Introspection mode
    options.IntrospectionClientId = "your-client-id";                     // Required for introspection
    options.IntrospectionClientSecret = "your-client-secret";             // Required for introspection
    options.IntrospectionCredentialTransmission = IntrospectionCredentialTransmission.BasicAuth; // Default: BasicAuth

    // Token validation settings
    options.ValidateIssuer = true;                                         // Default: true
    options.ValidateAudience = true;                                       // Default: true
    options.ValidateLifetime = true;                                       // Default: true
    options.ValidateIssuerSigningKey = true;                               // Default: true

    // JWT validation
    options.ValidAlgorithms = new[] { "RS256" };                           // Default: RS256
    options.TokenTypes = new[] { "at+jwt" };                               // Default: at+jwt
    options.IntrospectionTokenTypes = new[] { "access_token" };            // Optional

    // JWKS caching
    options.JwksCacheDurationHours = 24;                                   // Default: 24 hours
    options.JwksRefreshIntervalMinutes = 5;                                // Default: 5 minutes
    options.JwksAllowedHosts = new[] { "keys.guardhouse.cloud" };          // Optional

    // Introspection caching (micro-cache for burst traffic)
    options.IntrospectionCacheTtlSeconds = 5;                              // Default: 5 seconds
    options.IntrospectionNegativeCacheTtlSeconds = 10;                     // Default: 10 seconds

    // HTTPS and metadata
    options.RequireHttps = true;                                           // Default: true
    options.RequireHttpsMetadata = null;                                   // Default: derived from authority
    options.SaveToken = false;                                             // Default: false

    options.RequestTimeoutSeconds = 30;                                    // Default: 30
    options.MaxRetryAttempts = 3;                                          // Default: 3
    options.EnableDebug = false;                                           // Default: false
});
```

## Features

### Token Management

✅ **Automatic Token Caching** - Tokens cached in memory with configurable expiration buffer

✅ **Automatic Token Refresh** - Uses refresh tokens when available

✅ **HTTP Resilience** - Retry and timeout policies with Polly

✅ **Client Introspection** - Optional RFC 7662 introspection for debugging

### Resource Server Validation

✅ **JWT Signature Validation** - JWKS discovery with lazy refresh on unknown keys

✅ **Introspection Mode** - RFC 7662 validation with micro-cache and negative cache

✅ **Configurable Validation Rules** - Issuer, audience, lifetime, algorithms, token types

✅ **Backchannel Hardening** - Allowed hosts and HTTPS enforcement for metadata/JWKS

### Developer Experience

✅ **Configuration Validation** - Fails fast at startup for missing required settings

✅ **Dependency Injection** - AddGuardhouseClient, AddGuardhouseResource, AddGuardhouse

✅ **Logging Integration** - Debug logging for token and JWKS diagnostics

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

**JWT Signature Mode** (Default):
- Uses OpenID configuration and JWKS for token validation
- Automatic key rotation support with lazy refresh on unknown kid
- Validates signature, issuer, audience, lifetime, algorithm, token type

**Introspection Mode** (RFC 7662):
- Validates tokens via Guardhouse introspection endpoint
- Client credentials required for introspection calls
- Micro-cache for burst traffic handling with positive/negative TTLs

### Validation Checks

- Algorithm allowlist with explicit rejection of "none"
- Token type allowlist (default: `at+jwt`)
- Issuer and audience validation with normalized issuer matching
- Lifetime validation with clock skew
- HTTPS requirement and allowed host checks for metadata/JWKS/introspection

### JWKS Caching with Lazy Refresh

The SDK implements "Lazy Refresh on Unknown Key" strategy:

1. Extract `kid` (Key ID) from incoming JWT token header
2. Check local cache for key
3. If key exists: Validate signature (fast path)
4. If key missing: Trigger JWKS refresh via discovery or `/.well-known/jwks`
5. Re-check cache and validate with new keys

This ensures your API accepts valid tokens even after key rotation, while using configurable cache and refresh intervals for regular refresh behavior.

### Introspection Micro-Cache Strategy

The SDK implements a micro-cache for introspection results:

- **Active TTL**: 5 seconds (default)
- **Inactive TTL**: 10 seconds (default)
- **Purpose**: Handles burst traffic while keeping revocation checks near real-time

Use introspection mode when you need real-time revocation checking, and tune TTLs to balance freshness and throughput.

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

- .NET 6.0 or later (supports .NET 6.0, 7.0, 8.0, 9.0, 10.0)
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
3. Ensure client is enabled in Guardhouse Cloud
4. Verify token hasn't expired

**Error: "invalid_client" when using introspection**

If your identity server rejects Basic Authentication headers, use FormData credential transmission:

```csharp
builder.Services.AddGuardhouseResource(options =>
{
    options.ValidationMode = TokenValidationMode.Introspection;
    options.IntrospectionCredentialTransmission = IntrospectionCredentialTransmission.FormData;
});
```

This sends `client_id` and `client_secret` as form parameters instead of HTTP Basic Authentication.

### JWKS Refresh Issues

**Error: "kid not found" or signature validation fails**

1. Check your Authority URL is correct
2. Verify Guardhouse server is accessible
3. Check that `JwksRefreshIntervalMinutes` is appropriate (default 5)
4. Check application logs for JWKS refresh errors

## Documentation

- Guardhouse Documentation: https://guardhouse.cloud/docs
- GitHub Issues: https://github.com/legiosoft/guardhouse-sdk-dotnet/issues
- Deployment Guide: [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md)

## License

Licensed under Apache License 2.0

## Support

- Guardhouse Cloud: https://guardhouse.cloud
- Documentation: https://guardhouse.cloud/docs
- GitHub Repository: https://github.com/legiosoft/guardhouse-sdk-dotnet
