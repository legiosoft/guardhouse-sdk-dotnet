# Example Client Application

This example demonstrates how to use Guardhouse SDK as a **Client** application that requests access tokens and calls protected APIs.

## What This Example Shows

- **Client Configuration**: Setting up Guardhouse SDK with credentials
- **Token Management**: Requesting and caching access tokens
- **API Authentication**: Using tokens to call protected endpoints
- **Product CRUD Operations**: Create, read, update, and delete products
- **Authorization Policies**: Protecting endpoints with `[Authorize]` attribute

## Prerequisites

- .NET 8.0 SDK
- Guardhouse Cloud account
- Guardhouse Client ID and Client Secret

## Configuration

1. Copy this project and update `appsettings.json`:

```json
{
  "Guardhouse": {
    "Authority": "https://your-guardhouse-server.com",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret",
    "Scope": "api"
  }
}
```

2. Replace the placeholder values with your actual Guardhouse credentials.

## Running the Example

```bash
cd ExampleClient
dotnet run
```

The API will be available at:
- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:5001`

## API Endpoints

### Public Endpoints (No Authentication Required)

- `GET /api/products` - Get all products

### Protected Endpoints (Authentication Required)

- `GET /api/products/{id}` - Get a specific product
- `POST /api/products` - Create a new product
- `DELETE /api/products/{id}` - Delete a product

### Token Information

- `GET /api/tokeninfo/info` - Get information about the current authentication token

## Testing with Swagger

When running in Development mode, Swagger UI is available at:
```
https://localhost:5001/swagger
```

To test protected endpoints in Swagger:
1. Click the **"Authorize"** button (🔒) at the top right
2. Enter your Guardhouse client credentials
3. Click **"Authorize"** to authenticate
4. All protected endpoints will now include the access token

## How It Works

### 1. Client Setup

In `Program.cs`, we configure the Guardhouse SDK as a client:

```csharp
builder.Services.AddGuardhouseClient(options =>
{
    options.Authority = "https://your-guardhouse-server.com";
    options.ClientId = "your-client-id";
    options.ClientSecret = "your-client-secret";
    options.Scope = "api";
    options.EnableTokenCaching = true;
    options.EnableTokenRefresh = true;
});
```

### 2. Token Management

The SDK automatically handles:
- **Token Caching**: Stores tokens in memory to avoid unnecessary requests
- **Token Refresh**: Automatically refreshes tokens when they're about to expire
- **Retry Logic**: Retries failed requests with exponential backoff

### 3. Protected API Calls

In controllers, we inject `IGuardhouseTokenService` and use it to get access tokens:

```csharp
public class ProductsController : ControllerBase
{
    private readonly IGuardhouseTokenService _tokenService;

    public ProductsController(IProductService productService, IGuardhouseTokenService tokenService)
    {
        _tokenService = tokenService;
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Product>> Create([FromBody] CreateProductRequest request)
    {
        // Get access token (cached or requested)
        var accessToken = await _tokenService.GetAccessTokenAsync();
        
        // Create product
        var newProduct = _productService.Create(request);
        
        return CreatedAtAction(nameof(GetById), new { id = newProduct.Id }, newProduct);
    }
}
```

### 4. Authorization

The `[Authorize]` attribute protects endpoints. Only requests with valid Guardhouse tokens can access protected endpoints.

## Security Features

This example demonstrates the following security features:

✅ **Automatic Token Management** - No manual token handling required
✅ **Token Caching** - Reduces server load and improves performance
✅ **Automatic Refresh** - Tokens are refreshed before expiration
✅ **HTTP Resilience** - Built-in retry policies for transient failures
✅ **Secure Token Storage** - Tokens are stored securely in memory
✅ **Protected Endpoints** - Sensitive operations require valid authentication

## Next Steps

After running this example:

1. **Create a Resource Server**: See the ExampleResource project to learn how to protect APIs
2. **Configure Your Guardhouse Cloud**: Set up clients and APIs in Guardhouse Cloud
3. **Customize Token Handling**: Extend the example to handle specific token requirements
4. **Add Authorization Policies**: Implement role-based or scope-based access control

## Common Scenarios

### Calling External APIs

```csharp
public async Task<string> CallExternalApi()
{
    var accessToken = await _tokenService.GetAccessTokenAsync();
    
    using var client = new HttpClient();
    client.DefaultRequestHeaders.Authorization = 
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
    
    var response = await client.GetAsync("https://external-api.com/data");
    return await response.Content.ReadAsStringAsync();
}
```

### Batch Operations with Token Reuse

```csharp
public async Task ProcessMultipleRequests()
{
    var accessToken = await _tokenService.GetAccessTokenAsync();
    
    using var client = new HttpClient();
    client.DefaultRequestHeaders.Authorization = 
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
    
    // All requests use the same token
    var tasks = endpoints.Select(endpoint => 
        client.GetAsync(endpoint));
    
    await Task.WhenAll(tasks);
}
```

## Troubleshooting

### "401 Unauthorized" Errors

- Verify your Client ID and Secret are correct
- Check that the Authority URL is correct
- Ensure your client is enabled in Guardhouse Cloud

### Token Expiration Issues

- The SDK automatically refreshes tokens
- Enable `EnableTokenRefresh = true` in configuration
- Check your client's refresh token settings in Guardhouse Cloud

### CORS Issues

- Ensure Guardhouse Cloud allows your application's origin
- Add your development server URL to allowed origins

## Support

- Guardhouse SDK Documentation: https://docs.guardhouse.cloud
- GitHub Issues: https://github.com/guardhouse/guardhouse-sdk-dotnet/issues
- Guardhouse Cloud: https://guardhouse.cloud
