using ExampleClient.DTOs;
using Guardhouse.SDK.Services;
using System.Net.Http.Json;

namespace ExampleClient.Services;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public interface IProductService
{
    Task<IEnumerable<Product>> GetAll();
    Task<Product?> GetById(int id);
    Task<Product> Create(CreateProductRequest request);
    Task<bool> Delete(int id);
    Task<int> Count();
}

public class ProductService : IProductService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IGuardhouseTokenService _tokenService;
    private readonly string _resourceServerUrl;

    public ProductService(IHttpClientFactory httpClientFactory, IConfiguration configuration, IGuardhouseTokenService tokenService)
    {
        _httpClientFactory = httpClientFactory;
        _tokenService = tokenService;
        _resourceServerUrl = GetResourceServerUrl(configuration);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = _httpClientFactory.CreateClient();
        var accessToken = await _tokenService.GetAccessTokenAsync();
        client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    public async Task<IEnumerable<Product>> GetAll()
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.GetFromJsonAsync<ProductsResponse>($"{_resourceServerUrl}/api/products");
        return response?.Products ?? [];
    }

    public async Task<Product?> GetById(int id)
    {
        var client = await CreateAuthenticatedClientAsync();
        using var response = await client.GetAsync($"{_resourceServerUrl}/api/products/{id}");
        
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ProductResponse>();
        return payload?.Product;
    }

    public async Task<Product> Create(CreateProductRequest request)
    {
        var client = await CreateAuthenticatedClientAsync();
        using var response = await client.PostAsJsonAsync($"{_resourceServerUrl}/api/products", request);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ProductResponse>();
        return payload?.Product ?? throw new InvalidOperationException("Failed to create product");
    }

    public async Task<bool> Delete(int id)
    {
        var client = await CreateAuthenticatedClientAsync();
        using var response = await client.DeleteAsync($"{_resourceServerUrl}/api/products/{id}");
        
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return false;
        
        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task<int> Count()
    {
        var products = await GetAll();
        return products.Count();
    }

    private static string GetResourceServerUrl(IConfiguration configuration)
    {
        var resourceServerUrl = configuration["ResourceServer:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(resourceServerUrl))
        {
            throw new InvalidOperationException("ResourceServer:BaseUrl is required.");
        }

        if (!Uri.TryCreate(resourceServerUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("ResourceServer:BaseUrl must be an absolute URL.");
        }

        return resourceServerUrl;
    }

    private sealed class ProductsResponse
    {
        public List<Product> Products { get; set; } = [];
    }

    private sealed class ProductResponse
    {
        public Product? Product { get; set; }
    }
}
