using ExampleClient.DTOs;

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
    IEnumerable<Product> GetAll();
    Product? GetById(int id);
    Product Create(CreateProductRequest request);
    void Delete(int id);
    int Count();
}

public class ProductService : IProductService
{
    private static readonly List<Product> _products = new()
    {
        new() { Id = 1, Name = "Laptop", Price = 999.99m, Description = "High-performance laptop" },
        new() { Id = 2, Name = "Mouse", Price = 29.99m, Description = "Wireless mouse" },
        new() { Id = 3, Name = "Keyboard", Price = 79.99m, Description = "Mechanical keyboard" }
    };
    private static int _nextId = 4;

    public IEnumerable<Product> GetAll()
    {
        return _products;
    }

    public Product? GetById(int id)
    {
        return _products.FirstOrDefault(p => p.Id == id);
    }

    public Product Create(CreateProductRequest request)
    {
        var product = new Product
        {
            Id = _nextId++,
            Name = request.Name,
            Price = request.Price,
            Description = request.Description
        };
        _products.Add(product);
        return product;
    }

    public void Delete(int id)
    {
        var product = GetById(id);
        if (product != null)
        {
            _products.Remove(product);
        }
    }

    public int Count()
    {
        return _products.Count;
    }
}
