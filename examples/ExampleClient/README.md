# Example Client Application

This example demonstrates how to use Guardhouse SDK as a **Client** application that requests access tokens and uses them to call protected APIs.

## ⚠️ IMPORTANT: Configuration Required

**This example will NOT work with the default placeholder credentials.** You must configure your actual Guardhouse credentials before running.

### Before You Start

1. Log in to your [Guardhouse Cloud](https://guardhouse.cloud) account
2. Create a new client or use an existing one
3. Note your **Client ID** and **Client Secret**
4. Note your **Guardhouse Authority URL**

### Configuration Steps

Update the `appsettings.json` file with your credentials:

```json
{
  "Guardhouse": {
    "Authority": "https://your-actual-guardhouse-url.com",
    "ClientId": "your-actual-client-id",
    "ClientSecret": "your-actual-client-secret",
    "Scope": "api"
  }
}
```

**Replace the placeholder values:**
- `https://your-guardhouse-server.com` → Your actual Guardhouse instance URL
- `your-client-id` → Your actual Client ID from Guardhouse Cloud
- `your-client-secret` → Your actual Client Secret from Guardhouse Cloud
- `api` → The scope you want to request (must match what's configured in Guardhouse Cloud)

## What This Example Shows

- **Client Configuration**: Setting up Guardhouse SDK with client credentials
- **Token Management**: Requesting, caching, and refreshing access tokens
- **Token Introspection**: Checking token validity and metadata
- **Mock API Calls**: Demonstrating how to use access tokens when calling external APIs
- **Error Handling**: Graceful error handling for configuration and network issues

## Prerequisites

- .NET 8.0 SDK
- Guardhouse Cloud account
- Guardhouse Client ID and Client Secret
- Configured authority URL

## Running the Example

### Step 1: Configure Credentials

Edit `examples/ExampleClient/appsettings.json` and replace the placeholder values with your actual Guardhouse credentials.

### Step 2: Run the Application

```bash
cd examples/ExampleClient
dotnet run
```

The API will be available at:
- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:5001`

### Step 3: Test with Swagger UI

Open your browser and navigate to:
```
https://localhost:5001/swagger
```

## API Endpoints

### Token Management

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/token/current` | GET | Request a new access token |
| `/api/token/refresh` | GET | Refresh an existing token |
| `/api/token/introspect` | POST | Introspect a token to check validity |
| `/api/token/check-active` | GET | Check if a token is active |

### Products (Mock External API)

These endpoints demonstrate how to use access tokens when calling APIs:

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/products` | GET | Get all products (returns token in response) |
| `/api/products/{id}` | GET | Get a specific product by ID |
| `/api/products` | POST | Create a new product |
| `/api/products/{id}` | DELETE | Delete a product |

## Troubleshooting

### "Failed to request token" / "400 Bad Request"

**This is the most common error** and occurs when:

1. **Placeholder credentials still in use**
   - ✅ Solution: Update `appsettings.json` with your actual Guardhouse credentials

2. **Invalid authority URL**
   - ✅ Solution: Verify the Authority URL matches your Guardhouse instance

3. **Invalid client credentials**
   - ✅ Solution: Verify Client ID and Client Secret are correct (check for typos or extra spaces)

4. **Scope doesn't exist in Guardhouse Cloud**
   - ✅ Solution: Verify the scope is configured in your Guardhouse Cloud client settings

5. **Client not enabled in Guardhouse Cloud**
   - ✅ Solution: Ensure the client is active and enabled in Guardhouse Cloud

### "401 Unauthorized" Errors

- Verify your Client ID and Secret are correct
- Check that Authority URL is correct
- Ensure your client is enabled in Guardhouse Cloud
- Verify scope matches what's configured in Guardhouse Cloud

### Token Expiration Issues

- The SDK automatically refreshes tokens when `EnableTokenRefresh = true`
- Check your client's refresh token settings in Guardhouse Cloud
- Increase `CacheExpirationBufferSeconds` if you experience frequent expiration

### Connection Errors

- Verify Guardhouse server is accessible from your network
- Check firewall rules allow outbound HTTPS
- Verify SSL/TLS certificates are valid

## How It Works

### 1. Client Setup

In `Program.cs`, we configure Guardhouse SDK as a client:

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

**Automatic Caching:**
- Tokens are cached in memory
- Caching includes an expiration buffer (default: 60 seconds)
- Prevents unnecessary token requests

**Automatic Refresh:**
- Tokens are refreshed before expiration
- Uses refresh tokens when available
- Seamless to application code

**Token Introspection:**
- Check if a token is active: `await _tokenService.IsTokenActiveAsync(token)`
- Get full token metadata: `await _tokenService.IntrospectTokenAsync(token)`

### 4. Calling Protected APIs

When calling protected APIs, include access token in Authorization header:

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

This example demonstrates following security features:

✅ **Client Credentials Flow** - Secure client authentication
✅ **Automatic Token Caching** - Reduces server load and improves performance
✅ **Automatic Token Refresh** - Tokens are refreshed before expiration
✅ **HTTP Resilience** - Built-in retry policies for transient failures
✅ **Token Introspection** - Verify token validity without decoding
✅ **Error Handling** - Graceful error handling with clear messages
✅ **Secure Secret Storage** - Use environment variables or secret managers

## Best Practices

### Environment-Specific Configuration

For production, don't commit secrets to source control. Use environment variables or secret managers:

**Using Environment Variables:**

```bash
export Guardhouse__Authority="https://your-guardhouse-server.com"
export Guardhouse__ClientId="your-client-id"
export Guardhouse__ClientSecret="your-client-secret"
export Guardhouse__Scope="api"
```

**Using User Secrets:**

```bash
dotnet user-secrets init
dotnet user-secrets set "Guardhouse:Authority" "https://your-guardhouse-server.com"
dotnet user-secrets set "Guardhouse:ClientId" "your-client-id"
dotnet user-secrets set "Guardhouse:ClientSecret" "your-client-secret"
dotnet user-secrets set "Guardhouse:Scope" "api"
```

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

## Next Steps

After running this example:

1. **See ExampleResource**: Learn how to build protected API endpoints
2. **Configure Your Guardhouse Cloud**: Set up clients and APIs
3. **Implement Proper Secret Management**: Use environment variables or secret stores
4. **Add Error Handling**: Implement proper error handling for production
5. **Read the SDK Documentation**: https://docs.guardhouse.cloud

## Support

- Guardhouse SDK Documentation: https://docs.guardhouse.cloud
- GitHub Issues: https://github.com/guardhouse/guardhouse-sdk-dotnet/issues
- Guardhouse Cloud: https://guardhouse.cloud
