# Example Resource Server

This example demonstrates how to use Guardhouse SDK as a **Resource Server** that validates incoming tokens and protects API endpoints.

## What This Example Shows

- **Resource Server Configuration**: Setting up Guardhouse SDK to validate tokens
- **Token Validation**: Two validation modes (JWT Signature vs Introspection)
- **Authorization Policies**: Protecting endpoints with scopes and roles
- **Security Claims**: Extracting user information from validated tokens
- **Token Information**: Inspecting validated token claims

## Prerequisites

- .NET 8.0 SDK
- Guardhouse Cloud account
- Guardhouse Resource Server configuration (Audience, Authority)

## Configuration

### For JWT Signature Validation (Default)

Update `appsettings.json`:

```json
{
  "Guardhouse": {
    "Authority": "https://your-guardhouse-server.com",
    "Audience": "my_resource_api",
    "ValidationMode": "JwtSignature"
  }
}
```

### For Introspection Validation (RFC 7662)

Update `appsettings.json`:

```json
{
  "Guardhouse": {
    "Authority": "https://your-guardhouse-server.com",
    "Audience": "my_resource_api",
    "ValidationMode": "Introspection",
    "IntrospectionClientId": "your-introspection-client-id",
    "IntrospectionClientSecret": "your-introspection-client-secret"
  }
}
```

## Running the Example

```bash
cd ExampleResource
dotnet run
```

The API will be available at:
- HTTP: `http://localhost:5002`
- HTTPS: `https://localhost:5003`

## API Endpoints

### Public Endpoints (No Token Required)

- `GET /api/products` - Get all products (public)

### Protected Endpoints (Token Required)

- `GET /api/products/{id}` - Get a specific product (requires `read` scope)
- `POST /api/products` - Create a new product (requires `write` scope)
- `DELETE /api/products/{id}` - Delete a product (requires `admin` role)

### Token Information

- `GET /api/tokeninfo/info` - Get information about the validated token
- `GET /api/tokeninfo/user` - Get user claims from the validated token
- `GET /api/tokeninfo/validate` - Validate token (returns 401 if invalid)

## Testing with cURL

### 1. Get All Products (Public)

```bash
curl https://localhost:5003/api/products
```

### 2. Get Product by ID (Requires `read` scope)

```bash
curl -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
     https://localhost:5003/api/products/1
```

### 3. Create Product (Requires `write` scope)

```bash
curl -X POST https://localhost:5003/api/products \
     -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
     -H "Content-Type: application/json" \
     -d '{"name":"New Product","price":299.99}'
```

### 4. Delete Product (Requires `admin` role)

```bash
curl -X DELETE https://localhost:5003/api/products/1 \
     -H "Authorization: Bearer YOUR_ACCESS_TOKEN"
```

### 5. Get Token Info

```bash
curl -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
     https://localhost:5003/api/tokeninfo/info
```

## How It Works

### 1. Resource Server Setup

In `Program.cs`, we configure the Guardhouse SDK as a resource server:

```csharp
builder.Services.AddGuardhouseResource(options =>
{
    options.Authority = "https://your-guardhouse-server.com";
    options.Audience = "my_resource_api";
    options.ValidationMode = TokenValidationMode.JwtSignature;
    options.ValidateIssuer = true;
    options.ValidateAudience = true;
    options.ValidateLifetime = true;
    options.ValidAlgorithms = new[] { "RS256" };
});
```

### 2. Authentication Middleware

The SDK automatically handles token validation:

```csharp
app.UseAuthentication();
app.UseAuthorization();
```

### 3. Protecting Endpoints

Use `[Authorize]` attribute or policies:

```csharp
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    [HttpGet]
    public ActionResult GetAll()
    {
        // Public endpoint
        return Ok(_productService.GetAll());
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "ReadScope")]
    public ActionResult GetById(int id)
    {
        // Protected - requires 'read' scope
        var product = _productService.GetById(id);
        var subject = User.FindFirst("sub")?.Value;
        var scopes = User.FindAll("scope").Select(c => c.Value).ToList();
        
        return Ok(new { Product = product, Subject = subject, Scopes = scopes });
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminRole")]
    public ActionResult Delete(int id)
    {
        // Protected - requires 'admin' role
        var user = User.Identity?.Name;
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        
        _productService.Delete(id);
        return Ok(new { User = user, Roles = roles });
    }
}
```

### 4. Authorization Policies

Define policies in `Program.cs`:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ReadScope", policy =>
        policy.RequireClaim("scope", "read"));
    
    options.AddPolicy("WriteScope", policy =>
        policy.RequireClaim("scope", "write"));
    
    options.AddPolicy("AdminRole", policy =>
        policy.RequireRole("admin"));
});
```

### 5. Accessing Token Claims

After authentication, access claims via `User`:

```csharp
[HttpGet("info")]
[Authorize]
public ActionResult GetTokenInfo()
{
    return Ok(new
    {
        Subject = User.FindFirst("sub")?.Value,
        Issuer = User.FindFirst("iss")?.Value,
        Audience = User.FindAll("aud").Select(c => c.Value).ToList(),
        ExpiresAt = User.FindFirst("exp")?.Value,
        Scopes = User.FindAll("scope").Select(c => c.Value).ToList(),
        Roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList()
    });
}
```

## Token Validation Modes

### JWT Signature Validation (Default)

**How it works:**
- Uses JWKS endpoint to fetch public keys
- Validates token signature locally
- Validates issuer, audience, lifetime, algorithm, and token type
- Fast and scalable (no network calls per request)

**When to use:**
- High-throughput APIs
- Low latency requirements
- Standard OAuth 2.0 / OpenID Connect tokens

**Configuration:**
```csharp
options.ValidationMode = TokenValidationMode.JwtSignature;
options.Authority = "https://your-guardhouse-server.com";
options.Audience = "my_resource_api";
options.ValidAlgorithms = new[] { "RS256" };
```

### Introspection Validation (RFC 7662)

**How it works:**
- Calls Guardhouse introspection endpoint for each request
- Validates token via Guardhouse server
- Supports immediate token revocation
- Micro-caching for performance (default TTL: 30 seconds)

**When to use:**
- Need immediate token revocation
- Token introspection requirements
- Enhanced security controls

**Configuration:**
```csharp
options.ValidationMode = TokenValidationMode.Introspection;
options.Authority = "https://your-guardhouse-server.com";
options.Audience = "my_resource_api";
options.IntrospectionClientId = "your-introspection-client-id";
options.IntrospectionClientSecret = "your-introspection-client-secret";
options.IntrospectionCacheTtlSeconds = 30; // Micro-cache TTL
```

## Security Features

This example demonstrates the following security features:

✅ **JWT Signature Validation** - Validates token signature with RS256
✅ **Algorithm Enforcement** - Only RS256 algorithm allowed
✅ **Token Type Validation** - Validates `typ` header (JWT)
✅ **Issuer/Audience Validation** - Prevents confused deputy attacks
✅ **Lifetime Validation** - Rejects expired tokens
✅ **JWKS Caching** - Efficient key rotation handling
✅ **Introspection Support** - RFC 7662 token validation
✅ **Scope-Based Access** - Protects endpoints based on scopes
✅ **Role-Based Access** - Protects endpoints based on roles

## Complete Example Flow

### 1. Client Requests Token

```bash
# From ExampleClient or using curl
curl -X POST https://your-guardhouse-server.com/connect/token \
     -d "client_id=your-client-id" \
     -d "client_secret=your-client-secret" \
     -d "grant_type=client_credentials" \
     -d "scope=api.read api.write"
```

### 2. Client Calls Protected API

```bash
curl -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
     https://localhost:5003/api/products/1
```

### 3. Resource Server Validates Token

The SDK automatically:
1. Extracts token from Authorization header
2. Validates signature (or calls introspection)
3. Checks issuer, audience, expiration
4. Extracts claims for use in authorization
5. Allows or denies access

### 4. Response

```json
{
  "product": {
    "id": 1,
    "name": "Laptop",
    "price": 999.99
  },
  "user": "client-123",
  "subject": "client-123",
  "scopes": ["read", "write"],
  "message": "Product retrieved successfully (protected endpoint)"
}
```

## Real-World Use Cases

### REST API with Scope-Based Access

```csharp
[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "ReadScope")]
    public ActionResult GetAll() { }

    [HttpPost]
    [Authorize(Policy = "WriteScope")]
    public ActionResult Create() { }

    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminRole")]
    public ActionResult Delete(int id) { }
}
```

### User Context from Token

```csharp
public class DocumentService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public async Task<Document> CreateDocument(CreateDocumentRequest request)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var userId = httpContext.User.FindFirst("sub")?.Value;
        var tenantId = httpContext.User.FindFirst("tenant_id")?.Value;
        
        var document = new Document
        {
            CreatedBy = userId,
            TenantId = tenantId,
            Name = request.Name,
            Content = request.Content
        };
        
        await _repository.AddAsync(document);
        return document;
    }
}
```

### Audit Logging

```csharp
public class AuditLoggingMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        var user = context.User;
        var subject = user.FindFirst("sub")?.Value;
        var scopes = user.FindAll("scope").Select(c => c.Value).ToList();
        
        _logger.LogInformation("User {Subject} with scopes {Scopes} accessed {Path}", 
            subject, string.Join(", ", scopes), context.Request.Path);
        
        await _next(context);
    }
}
```

## Troubleshooting

### "401 Unauthorized" Errors

- Verify the Authority URL is correct
- Check the Audience matches your Guardhouse resource configuration
- Ensure token hasn't expired
- Verify token includes required scopes/roles
- Check ValidationMode configuration

### JWKS Issues (JWT Signature Mode)

- Verify Guardhouse JWKS endpoint is accessible
- Check `JwksCacheDurationHours` and `JwksRefreshIntervalMinutes`
- Verify token's `kid` matches a key in JWKS
- Check application logs for JWKS refresh errors

### Introspection Issues (Introspection Mode)

- Verify IntrospectionClientId and IntrospectionClientSecret are correct
- Ensure introspection client is enabled in Guardhouse Cloud
- Check introspection endpoint is accessible
- Verify introspection cache TTL is appropriate

### Policy Not Matching

- Verify token includes required scopes
- Check scope names match exactly (case-sensitive)
- Verify role claims are in correct format
- Use `/api/tokeninfo/info` to inspect token claims

## Next Steps

After running this example:

1. **See ExampleClient**: Learn how to build a client that requests tokens
2. **Configure Your Guardhouse Cloud**: Set up clients and resources
3. **Implement Advanced Policies**: Custom policy requirements
4. **Add Rate Limiting**: Protect against abuse
5. **Implement Refresh Tokens**: For long-lived sessions

## Support

- Guardhouse SDK Documentation: https://docs.guardhouse.cloud
- GitHub Issues: https://github.com/guardhouse/guardhouse-sdk-dotnet/issues
- Guardhouse Cloud: https://guardhouse.cloud
