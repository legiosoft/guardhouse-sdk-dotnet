# Guardhouse .NET Example Client

This is a complete example demonstrating how to use the Guardhouse SDK in a .NET Web API application.

## Features

- **OpenID Connect Client Integration**: Full OAuth2/OpenID Connect client functionality using Guardhouse SDK
- **JWT Bearer Authentication**: Protect API endpoints with JWT token validation
- **Authorization Policies**: Multiple authorization policies based on scopes and roles
- **Swagger with OAuth2**: Interactive API documentation with OAuth2 authorization
- **Token Management**: Automatic token request, caching, and refresh
- **Clean Architecture**: Organized folder structure following 1 class = 1 file rule

## Project Structure

```
ExampleClient/
├── Controllers/               # API Controllers
│   ├── WeatherController.cs      # Public and protected weather endpoints
│   ├── UsersController.cs         # User profile and admin endpoints
│   ├── ProductsController.cs      # Products CRUD with different access levels
│   └── TokenInfoController.cs     # Token and claims inspection endpoints
├── DTOs/                     # Data Transfer Objects
│   └── CreateProductRequest.cs    # Product creation DTO
├── Extensions/              # Extension methods for service configuration
│   ├── SwaggerExtensions.cs       # Swagger/OpenAPI configuration
│   ├── AuthorizationExtensions.cs # Authorization policies setup
│   └── GuardhouseExtensions.cs    # Guardhouse SDK configuration
├── Models/                   # Domain models
│   ├── Product.cs                  # Product entity
│   └── WeatherForecast.cs          # Weather forecast model
├── Services/                 # Business logic and data access
│   └── ProductService.cs           # Product service interface and implementation
├── Program.cs                # Application entry point
├── appsettings.json         # Configuration file
├── Properties/
│   └── launchSettings.json  # Launch profiles
└── ExampleClient.csproj     # Project file
```

## Architecture Principles

This example follows these best practices:

- **1 Class = 1 File Rule**: Each class is in its own file for better maintainability
- **Separation of Concerns**: Controllers, Services, DTOs, and Models are separated
- **Extension Methods**: Configuration logic is encapsulated in extension methods
- **Dependency Injection**: Services are registered in DI container and injected via constructors

## Configuration

Update the `appsettings.json` file with your Guardhouse server details:

```json
{
  "Guardhouse": {
    "Authority": "https://your-guardhouse-server.com",
    "ClientId": "example-client",
    "ClientSecret": "your-client-secret-here",
    "Scope": "api",
    "Audience": "api"
  }
}
```

## Running the Example

1. **Restore dependencies:**

   ```bash
   cd examples/ExampleClient
   dotnet restore
   ```

2. **Build the project:**

   ```bash
   dotnet build
   ```

3. **Run the application:**

   ```bash
   dotnet run
   ```

4. **Access Swagger UI:**

   Open your browser and navigate to:
   - HTTP: `http://localhost:5000/swagger`
   - HTTPS: `https://localhost:5001/swagger`

## API Endpoints

### Weather Endpoints

- `GET /api/weather` - Public endpoint (no authentication required)
- `GET /api/weather/protected` - Protected endpoint (authentication required)

### User Endpoints

- `GET /api/users/profile` - Requires `read` or `api` scope
- `GET /api/users/all` - Requires `admin` scope

### Product Endpoints

- `GET /api/products` - Public endpoint
- `GET /api/products/{id}` - Requires `read` or `api` scope
- `POST /api/products` - Requires `write` or `admin` scope
- `DELETE /api/products/{id}` - Requires `admin` scope

### Token Info Endpoints

- `GET /api/tokeninfo/claims` - View all claims in the JWT token
- `GET /api/tokeninfo/user-info` - View user information from token
- `GET /api/tokeninfo/authorize-info` - View authorization details

## Using Swagger with OAuth2

1. Click the **Authorize** button (🔒) in Swagger UI
2. Enter your Guardhouse client credentials
3. Click **Authorize** to authenticate
4. The access token will be included in all subsequent API requests

## Authorization Policies

### ReadAccess
Requires `read` or `api` scope. Use for read operations.

### WriteAccess
Requires `write` or `admin` scope. Use for write operations.

### AdminOnly
Requires `admin` scope. Use for administrative operations.

## How It Works

### Guardhouse SDK Setup

The application uses extension methods to configure Guardhouse:

```csharp
builder.Services.AddCustomGuardhouse(builder.Configuration);
```

This internally calls:
- `AddCustomGuardhouseClient` - For token management
- `AddCustomGuardhouseResource` - For API protection

### Swagger Configuration

Swagger is configured using an extension method in `SwaggerExtensions.cs`:

```csharp
builder.Services.AddCustomSwaggerGen();
```

This includes OAuth2 security scheme and scopes configuration.

### Authorization Policies

Authorization policies are configured in `AuthorizationExtensions.cs`:

```csharp
builder.Services.AddCustomAuthorization();
```

### Service Layer

The `ProductsController` demonstrates clean separation of concerns by using `IProductService`:

```csharp
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly IGuardhouseTokenService _tokenService;

    public ProductsController(IProductService productService, IGuardhouseTokenService tokenService)
    {
        _productService = productService;
        _tokenService = tokenService;
    }
}
```

### Token Usage

The `ProductsController` demonstrates how to use the token service to get access tokens for downstream API calls:

```csharp
public async Task<ActionResult<Product>> Create([FromBody] CreateProductRequest request)
{
    var newProduct = _productService.Create(request);

    var accessToken = await _tokenService.GetAccessTokenAsync();
    
    return CreatedAtAction(nameof(GetById), new { id = newProduct.Id }, new
    {
        Product = newProduct,
        CurrentAccessToken = accessToken.Substring(0, 20) + "..."
    });
}
```

### Authorization

Controllers use the `[Authorize]` attribute with policies:

```csharp
[Authorize(Policy = "ReadAccess")]
public ActionResult GetUserProfile()
{
    // Only users with 'read' or 'api' scope can access
}

[Authorize(Policy = "AdminOnly")]
public ActionResult Delete(int id)
{
    // Only users with 'admin' scope can access
}
```

## Extension Methods

### SwaggerExtensions
- `AddCustomSwaggerGen()` - Configures Swagger with OAuth2 support

### AuthorizationExtensions
- `AddCustomAuthorization()` - Sets up authorization policies

### GuardhouseExtensions
- `AddCustomGuardhouseClient()` - Configures Guardhouse client for token management
- `AddCustomGuardhouseResource()` - Configures Guardhouse resource server for API protection
- `AddCustomGuardhouse()` - Configures both client and resource in one call

## Required Scopes

Make sure your Guardhouse client is configured with the following scopes:

- `api` - Basic API access
- `read` - Read operations
- `write` - Write operations
- `admin` - Administrative operations

## Troubleshooting

### 401 Unauthorized

- Ensure you've authenticated in Swagger UI
- Check that your token has the required scope for the endpoint
- Verify the Guardhouse authority URL is correct

### 403 Forbidden

- Your token is valid but lacks the required scope
- Check the `/api/tokeninfo/authorize-info` endpoint to see your current scopes

### Connection Issues

- Ensure your Guardhouse server is accessible
- Check firewall and network settings
- Verify the Authority URL in `appsettings.json`

## Learn More

- [Guardhouse SDK Documentation](../../README.md)
- [Guardhouse Official Documentation](https://docs.guardhouse.com)
- [.NET Authentication Documentation](https://docs.microsoft.com/aspnet/core/security/authentication/)

## License

This example is part of the Guardhouse SDK and is licensed under the MIT License.
