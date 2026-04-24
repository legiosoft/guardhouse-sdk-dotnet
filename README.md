# Guardhouse SDK for .NET

Official .NET SDK for Guardhouse Cloud.

Use it to:

- request access tokens with the client credentials flow
- validate incoming tokens with JWT signature validation or introspection
- call the external Guardhouse system API for users, roles, and permissions

Supported frameworks:

- .NET 6.0
- .NET 7.0
- .NET 8.0
- .NET 9.0
- .NET 10.0

## Installation

```bash
dotnet add package Guardhouse.SDK
```

## What The SDK Includes

- `IGuardhouseTokenService` for token acquisition, refresh, and introspection
- `IGuardhouseResourceService` for resource-server token validation
- `IGuardhouseUsersClient`, `IGuardhouseRolesClient`, and `IGuardhousePermissionsClient` for the external system API
- DI extensions for client, resource server, and system API registration
- HTTP resilience with Polly
- JWKS caching with lazy refresh

## Core Capabilities

- Client credentials token acquisition with in-memory token caching
- Optional refresh-token support through `offline_access`
- JWT signature validation using OpenID Connect metadata and JWKS
- RFC 7662 token introspection for resource-server validation and client-side inspection
- Typed external system API clients for users, roles, and permissions
- Retry and timeout policies for outbound HTTP calls

## Quick Start

### Client Application

```csharp
using Guardhouse.SDK.Extensions;
using Guardhouse.SDK.Services;

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

app.MapGet("/token", async (IGuardhouseTokenService tokenService) =>
{
    var accessToken = await tokenService.GetAccessTokenAsync();
    return Results.Ok(new
    {
        AccessTokenPreview = accessToken[..Math.Min(20, accessToken.Length)] + "..."
    });
});

app.Run();
```

### System API

Use this when your application needs to call the external Guardhouse endpoints under `Users`, `Roles`, and `Permissions`.

```csharp
using Guardhouse.SDK.Constants;
using Guardhouse.SDK.Extensions;
using Guardhouse.SDK.Models.Users;
using Guardhouse.SDK.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGuardhouseClientWithApiClients(
    authority: "https://your-guardhouse-server.com",
    clientId: "your-system-api-client-id",
    clientSecret: "your-system-api-client-secret",
    scope: AuthorizationConsts.Scopes.SystemApi,
    apiBaseUrl: "https://your-guardhouse-server.com");

var app = builder.Build();

app.MapPost("/users", async (IGuardhouseUsersClient usersClient) =>
{
    var created = await usersClient.CreateUserAsync(new CreateUserRequest
    {
        FirstName = "Ada",
        LastName = "Lovelace",
        Email = "ada@example.com",
        SendInvite = true,
        InviterName = "Grace Hopper",
        RedirectUrl = "https://your-app.example.com/invitation"
    });

    return Results.Ok(created);
});

app.MapGet("/users/{id:int}", async (int id, IGuardhouseUsersClient usersClient) =>
{
    var user = await usersClient.GetUserByIdAsync(id);
    return user is null ? Results.NotFound() : Results.Ok(user);
});

app.Run();
```

For the full endpoint-to-client mapping, see the [System API Guide](https://github.com/legiosoft/guardhouse-sdk-dotnet/blob/main/docs/SYSTEM_API.md).

### Resource Server

```csharp
using Guardhouse.SDK.Constants;
using Guardhouse.SDK.Extensions;
using Guardhouse.SDK.Models;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(GuardhouseConstants.Authentication.DefaultScheme);
builder.Services.AddGuardhouseResource(options =>
{
    options.Authority = "https://your-guardhouse-server.com";
    options.Audience = "my_resource_api";
    options.ValidationMode = TokenValidationMode.JwtSignature;
});
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/protected", [Authorize] () => Results.Ok(new { Message = "Protected" }));

app.Run();
```

For introspection mode:

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

Validation modes:

- `TokenValidationMode.JwtSignature` validates JWTs locally using discovered signing keys
- `TokenValidationMode.Introspection` validates tokens through the Guardhouse introspection endpoint

## Registration Helpers

Client registration:

- `AddGuardhouseClient(...)`
- `AddGuardhouse(...)`

System API registration:

- `AddGuardhouseApiClients(...)`
- `AddGuardhouseClientWithApiClients(...)`
- `AddGuardhouseUserClients(...)`
- `AddGuardhouseClientWithUserClients(...)`

Resource server registration:

- `AddGuardhouseResource(...)`

Backward-compatible aliases are still available for the older user-service naming.

Repeated calls to the Guardhouse registration helpers are safe. The SDK adds its internal infrastructure once, while later `configureAction` delegates still layer option values.

## Main Interfaces

Token client:

```csharp
public interface IGuardhouseTokenService
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
    Task<TokenResponse> RequestTokenAsync(CancellationToken cancellationToken = default);
    Task<TokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<IntrospectionResponse> IntrospectTokenAsync(string tokenValue, CancellationToken cancellationToken = default);
    Task<bool> IsTokenActiveAsync(string tokenValue, CancellationToken cancellationToken = default);
}
```

Resource service:

```csharp
public interface IGuardhouseResourceService
{
    Task<ClaimsPrincipal?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<IntrospectionResponse> IntrospectTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<AuthenticationScheme?> GetDefaultSchemeAsync(CancellationToken cancellationToken = default);
}
```

System API clients:

- `IGuardhouseUsersClient` for create/get/update user operations, password changes, role assignment, block/unblock, and personal-data deletion
- `IGuardhouseRolesClient` for create/get/update role operations and role-permission assignment
- `IGuardhousePermissionsClient` for create/get/update permission operations
- `IGuardhouseUserService` remains available as a backward-compatible alias of `IGuardhouseUsersClient`

Detailed system API usage and endpoint mapping are documented in the [System API Guide](https://github.com/legiosoft/guardhouse-sdk-dotnet/blob/main/docs/SYSTEM_API.md).

## Configuration Notes

- `Authority` is required for token acquisition and resource-server validation.
- `ApiBaseUrl` is the base URL for the external system API.
- If `ApiBaseUrl` is omitted, the SDK falls back to `Authority`.
- The intended scope for the system API is `AuthorizationConsts.Scopes.SystemApi` (`system_api`).
- For refresh tokens, enable `IncludeOfflineAccessScope` or include `offline_access` explicitly in `Scope`.
- You can register Guardhouse services from multiple startup paths without duplicating the SDK's internal DI infrastructure.

## Security And Reliability

- JWT validation uses issuer, audience, lifetime, and signing-key checks from Guardhouse metadata
- JWKS refresh is lazy and triggered when unknown signing keys are encountered
- Introspection mode does not silently fall back to signature validation
- HTTP calls use retry and timeout policies to handle transient failures
- Error messages sanitize server responses instead of returning raw bodies

## System API Coverage

The SDK system API surface is intentionally limited to the external routes exposed by Guardhouse under:

- `Endpoints/API/Users`
- `Endpoints/API/Roles`
- `Endpoints/API/Permissions`

If a route is not part of those external endpoint folders, it is not part of the supported SDK system API contract.

## Examples

- Example client app: [examples/ExampleClient](https://github.com/legiosoft/guardhouse-sdk-dotnet/tree/main/examples/ExampleClient)
- Example resource app: [examples/ExampleResource](https://github.com/legiosoft/guardhouse-sdk-dotnet/tree/main/examples/ExampleResource)

## Documentation

- Guardhouse Documentation: [guardhouse.cloud/docs](https://guardhouse.cloud/docs/getting-started)
- System API Guide: [docs/SYSTEM_API.md](https://github.com/legiosoft/guardhouse-sdk-dotnet/blob/main/docs/SYSTEM_API.md)
- Deployment Guide: [docs/DEPLOYMENT.md](https://github.com/legiosoft/guardhouse-sdk-dotnet/blob/main/docs/DEPLOYMENT.md)
- GitHub Issues: [guardhouse-sdk-dotnet/issues](https://github.com/legiosoft/guardhouse-sdk-dotnet/issues)

## License

Apache-2.0
