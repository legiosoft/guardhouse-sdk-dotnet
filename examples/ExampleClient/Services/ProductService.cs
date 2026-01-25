using ExampleClient.DTOs;
using Guardhouse.SDK.Services;
using System.Text.Json;

namespace ExampleClient.Services;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? Description { get; set; }
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
    private readonly IConfiguration _configuration;
    private readonly IGuardhouseTokenService _tokenService;

    private string ResourceServerUrl => _configuration["ResourceServer:BaseUrl"]!;

    public ProductService(IHttpClientFactory httpClientFactory, IConfiguration configuration, IGuardhouseTokenService tokenService)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _tokenService = tokenService;
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
        var response = await client.GetAsync($"{ResourceServerUrl}/api/products");
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync();
        var document = JsonDocument.Parse(json);
        
        if (document.RootElement.TryGetProperty("products", out var productsElement))
        {
            var products = new List<Product>();
            foreach (var item in productsElement.EnumerateArray())
            {
                products.Add(new Product
                {
                    Id = item.GetProperty("id").GetInt32(),
                    Name = item.GetProperty("name").GetString() ?? string.Empty,
                    Price = item.GetProperty("price").GetDecimal(),
                    Description = item.TryGetProperty("description", out var desc) ? desc.GetString() : null
                });
            }
            return products;
        }
        
        return new List<Product>();
    }

    public async Task<Product?> GetById(int id)
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.GetAsync($"{ResourceServerUrl}/api/products/{id}");
        
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var document = JsonDocument.Parse(json);
        
        if (document.RootElement.TryGetProperty("product", out var productElement))
        {
            return new Product
            {
                Id = productElement.GetProperty("id").GetInt32(),
                Name = productElement.GetProperty("name").GetString() ?? string.Empty,
                Price = productElement.GetProperty("price").GetDecimal(),
                Description = productElement.TryGetProperty("description", out var desc) ? desc.GetString() : null
            };
        }
        
        return null;
    }

    public async Task<Product> Create(CreateProductRequest request)
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.PostAsJsonAsync($"{ResourceServerUrl}/api/products", request);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var document = JsonDocument.Parse(json);
        
        if (document.RootElement.TryGetProperty("product", out var productElement))
        {
            return new Product
            {
                Id = productElement.GetProperty("id").GetInt32(),
                Name = productElement.GetProperty("name").GetString() ?? string.Empty,
                Price = productElement.GetProperty("price").GetDecimal(),
                Description = productElement.TryGetProperty("description", out var desc) ? desc.GetString() : null
            };
        }
        
        throw new InvalidOperationException("Failed to create product");
    }

    public async Task<bool> Delete(int id)
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.DeleteAsync($"{ResourceServerUrl}/api/products/{id}");
        
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
}
