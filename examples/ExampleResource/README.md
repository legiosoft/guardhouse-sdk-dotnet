# Example Resource Server

This project shows how to use Guardhouse SDK in an ASP.NET Core Web API as a resource server.

## What this sample includes

- Guardhouse resource server setup with `AddGuardhouseResource(...)`
- JWT signature validation (default) and introspection validation
- Swagger UI bearer token authorization
- Protected product endpoints
- Custom SA policy based on claim `system=system_administrator`
- Token introspection endpoint exposed by the resource API
- Debug endpoints for local troubleshooting

## Prerequisites

- .NET 8 SDK
- Guardhouse authority URL
- Guardhouse resource audience
- (If using introspection) introspection client credentials

## Configuration

`Program.cs` maps `Guardhouse` settings explicitly (not full section bind).

Update `appsettings.json`:

```json
{
  "Guardhouse": {
    "Authority": "https://your-guardhouse-server.com",
    "Audience": "my_resource_api",
    "ValidationMode": "JwtSignature",
    "IntrospectionClientId": "your-introspection-client-id",
    "IntrospectionClientSecret": "your-introspection-client-secret",
    "IntrospectionCredentialTransmission": "FormData",
    "RequestTimeoutSeconds": 30,
    "EnableDebug": true
  }
}
```

Notes:

- `ValidationMode`:
  - `JwtSignature` = local JWT validation
  - `Introspection` = `/connect/introspect` call
- `IntrospectionClientId` and `IntrospectionClientSecret` are required for introspection mode.
- SDK default for introspection credentials is `FormData`.
- In Guardhouse setups, introspection `client_id` usually should match resource `Audience`.

## Run

```bash
cd examples/ExampleResource
dotnet run --launch-profile https
```

Default URLs for the `https` profile:

- `https://localhost:5001`
- `http://localhost:5000`

Swagger UI:

- `https://localhost:5001/swagger`

## Swagger authorization

1. Open `/swagger`
2. Click **Authorize**
3. Enter `Bearer YOUR_ACCESS_TOKEN`
4. Call secured endpoints

## API endpoints

### Product endpoints

- `GET /api/products` - anonymous
- `GET /api/products/{id}` - requires authenticated token (`[Authorize]`)
- `POST /api/products` - requires policy `AuthorizationConsts.Policies.SA`
- `DELETE /api/products/{id}` - requires policy `AuthorizationConsts.Policies.SA`

### Token endpoints

- `GET /api/tokeninfo/info` - requires authenticated token
- `GET /api/tokeninfo/user` - requires authenticated token
- `GET /api/tokeninfo/validate` - checks for Bearer header presence (debug helper)
- `POST /api/tokeninfo/introspect` - anonymous; resource server introspects token using configured introspection credentials

### Debug endpoints

- `GET /api/debug/config`
- `GET /api/debug/auth-header`
- `GET /api/debug/decode-token?token=...`
- `GET /api/debug/user-claims` (authorized)
- `GET /api/debug/check-expiry?token=...`

## Authorization policy in this sample

`Program.cs` defines one custom policy:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationConsts.Policies.SA, policy =>
        policy.RequireAssertion(context => IsSystemAdministrator(context.User)));
});

static bool IsSystemAdministrator(ClaimsPrincipal user)
{
    return user.FindAll(AuthorizationConsts.ClaimTypes.System)
        .SelectMany(claim => claim.Value.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .Any(claimValue => string.Equals(
            claimValue,
            AuthorizationConsts.ClaimValues.SystemAdministrator,
            StringComparison.OrdinalIgnoreCase));
}
```

This aligns with identities that emit custom claims like:

- claim type `system`
- value `system_administrator`

## Claims access (native APIs)

The sample uses native `ClaimsPrincipal` APIs (`FindAll`, `FindFirst`, LINQ over `User.Claims`).

Example scope extraction:

```csharp
var scopes = User.FindAll("scope")
    .Concat(User.FindAll("scp"))
    .SelectMany(claim => claim.Value.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToList();
```

Example custom claim extraction:

```csharp
var systemClaims = User.FindAll(AuthorizationConsts.ClaimTypes.System)
    .SelectMany(claim => claim.Value.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToList();
```

## Introspection endpoint behavior

`POST /api/tokeninfo/introspect` accepts token either:

- from JSON body: `{ "token": "..." }`
- or from `Authorization: Bearer ...` header

Response includes raw introspection fields and parsed values (`ParsedScopes`, `ParsedRoles`, `ParsedAudiences`, `ParsedClaims`).

Error responses:

- `400` when token is missing
- `502` on introspection/provider/configuration failure
- `504` on introspection timeout

## Quick cURL checks

Public products:

```bash
curl https://localhost:5001/api/products
```

Authorized product by id:

```bash
curl -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  https://localhost:5001/api/products/1
```

Create product (SA policy required):

```bash
curl -X POST https://localhost:5001/api/products \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name":"New Product","price":299.99}'
```

Introspect token via resource API:

```bash
curl -X POST https://localhost:5001/api/tokeninfo/introspect \
  -H "Content-Type: application/json" \
  -d '{"token":"YOUR_ACCESS_TOKEN"}'
```

## Troubleshooting

- `401 Unauthorized`:
  - verify `Authority` and `Audience`
  - ensure token is valid and not expired
  - ensure required claims/policy data are present
- Introspection timeout:
  - verify `Guardhouse:Authority` reachability
  - increase `Guardhouse:RequestTimeoutSeconds`
- SA policy does not match:
  - verify token includes `system=system_administrator`
- Introspection mode warning about `IntrospectionClientId` vs `Audience`:
  - in Guardhouse, these usually should match
