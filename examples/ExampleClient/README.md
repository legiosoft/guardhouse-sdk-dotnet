# Example Client Application

This example demonstrates how to use Guardhouse SDK as a **Client** application that requests access tokens and uses them to call protected APIs.

## What This Example Shows

- **Client Configuration**: Setting up Guardhouse SDK with client credentials
- **Token Management**: Requesting, caching, and refreshing access tokens
- **Token Introspection**: Checking token validity and metadata
- **Mock API Calls**: Demonstrating how to use access tokens when calling external APIs

## Prerequisites

- .NET 8.0 SDK
- Guardhouse Cloud account
- Guardhouse Client ID and Client Secret

## Configuration

1. Update `appsettings.json` with your Guardhouse credentials:

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

### Token Management

- `GET /api/token/current` - Request a new access token
- `GET /api/token/refresh` - Refresh an existing token
- `POST /api/token/introspect` - Introspect a token to check its validity
- `GET /api/token/check-active?token={token}` - Check if a token is active

### Products (Mock External API)

These endpoints demonstrate how to use access tokens when calling APIs:

- `GET /api/products` - Get all products (returns token in response)
- `GET /api/products/{id}` - Get a specific product by ID
- `POST /api/products` - Create a new product
- `DELETE /api/products/{id}` - Delete a product

## Testing with Swagger

When running in Development mode, Swagger UI is available at:
```
https://localhost:5001/swagger
```

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

### 2. Getting an Access Token

The SDK automatically handles token caching and refresh:

```csharp
public class ProductsController : ControllerBase
{
    private readonly IGuardhouseTokenService _tokenService;

    public ProductsController(IGuardhouseTokenService tokenService)
    {
        _tokenService = tokenService;
    }

    [HttpGet]
    public async Task<ActionResult> GetAll()
    {
        // Get access token (cached or requested)
        var accessToken = await _tokenService.GetAccessTokenAsync();
        
        // Use token to call external API
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        
        var response = await client.GetAsync("https://external-api.com/products");
        return Ok(await response.Content.ReadAsStringAsync());
    }
}
```

### 3. Token Features

**Automatic Caching**:
- Tokens are cached in memory
- Caching includes an expiration buffer (default: 60 seconds)
- Prevents unnecessary token requests

**Automatic Refresh**:
- Tokens are refreshed before expiration
- Uses refresh tokens when available
- Seamless to the application code

**Token Introspection**:
- Check if a token is active: `await _tokenService.IsTokenActiveAsync(token)`
- Get full token metadata: `await _tokenService.IntrospectTokenAsync(token)`

### 4. Calling Protected APIs

When calling protected APIs, include the access token in the Authorization header:

```csharp
public async Task<string> CallProtectedApi()
{
    var accessToken = await _tokenService.GetAccessTokenAsync();
    
    using var client = new HttpClient();
    client.DefaultRequestHeaders.Authorization = 
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
    
    var response = await client.GetAsync("https://protected-api.com/data");
    return await response.Content.ReadAsStringAsync();
}
```

## Example: Token Management Flow

1. **Request Initial Token**:
   ```bash
   curl https://localhost:5001/api/token/current
   ```

2. **Use Token to Call API**:
   ```bash
   curl -H "Authorization: Bearer YOUR_TOKEN" \
        https://external-api.com/products
   ```

3. **Check Token Status**:
   ```bash
   curl -X POST https://localhost:5001/api/token/introspect \
        -H "Content-Type: application/json" \
        -d '{"token":"YOUR_TOKEN"}'
   ```

## Security Features

This example demonstrates the following security features:

✅ **Client Credentials Flow** - Secure client authentication
✅ **Automatic Token Caching** - Reduces server load and improves performance
✅ **Automatic Token Refresh** - Tokens are refreshed before expiration
✅ **HTTP Resilience** - Built-in retry policies for transient failures
✅ **Token Introspection** - Verify token validity without decoding
✅ **Secure Secret Storage** - Use environment variables or secret managers

## Real-World Use Cases

### Backend Service Calling Multiple APIs

```csharp
public class ExternalApiService
{
    private readonly IGuardhouseTokenService _tokenService;

    public async Task<CombinedData> FetchAllData()
    {
        var accessToken = await _tokenService.GetAccessTokenAsync();
        
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        
        var tasks = new[]
        {
            client.GetAsync("https://api1.example.com/data"),
            client.GetAsync("https://api2.example.com/data"),
            client.GetAsync("https://api3.example.com/data")
        };
        
        await Task.WhenAll(tasks);
        // Process responses...
    }
}
```

### Background Worker with Periodic Calls

```csharp
public class DataSyncWorker : BackgroundService
{
    private readonly IGuardhouseTokenService _tokenService;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var accessToken = await _tokenService.GetAccessTokenAsync();
            
            await SyncDataWithToken(accessToken);
            
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
```

## Troubleshooting

### "401 Unauthorized" Errors

- Verify your Client ID and Secret are correct
- Check that the Authority URL is correct
- Ensure your client is enabled in Guardhouse Cloud
- Verify the scope matches what's configured in Guardhouse Cloud

### Token Expiration Issues

- The SDK automatically refreshes tokens when `EnableTokenRefresh = true`
- Check your client's refresh token settings in Guardhouse Cloud
- Increase `CacheExpirationBufferSeconds` if you experience frequent expiration

### Connection Errors

- Verify Guardhouse server is accessible from your network
- Check firewall rules allow outbound HTTPS
- Verify SSL/TLS certificates are valid

## Next Steps

After running this example:

1. **See ExampleResource**: Learn how to build protected API endpoints
2. **Configure Your Guardhouse Cloud**: Set up clients and APIs
3. **Implement Proper Secret Management**: Use environment variables or secret stores
4. **Add Error Handling**: Implement proper error handling for production

## Support

- Guardhouse SDK Documentation: https://docs.guardhouse.cloud
- GitHub Issues: https://github.com/guardhouse/guardhouse-sdk-dotnet/issues
- Guardhouse Cloud: https://guardhouse.cloud
