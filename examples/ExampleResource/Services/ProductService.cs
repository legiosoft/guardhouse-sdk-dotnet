using ExampleResource.DTOs;
using ExampleResource.Models;

namespace ExampleResource.Services;

public interface IProductService
{
    IEnumerable<Product> GetAll();
    Product? GetById(int id);
    Product Create(CreateProductRequest request);
    bool Delete(int id);
    int Count();
}

public class ProductService : IProductService
{
    private static readonly List<Product> Products = new()
    {
        new() { Id = 1, Name = "Laptop", Price = 999.99m },
        new() { Id = 2, Name = "Phone", Price = 699.99m },
        new() { Id = 3, Name = "Tablet", Price = 449.99m }
    };

    public IEnumerable<Product> GetAll()
    {
        return Products;
    }

    public Product? GetById(int id)
    {
        return Products.FirstOrDefault(p => p.Id == id);
    }

    public Product Create(CreateProductRequest request)
    {
        var newProduct = new Product
        {
            Id = Products.Max(p => p.Id) + 1,
            Name = request.Name,
            Price = request.Price
        };
        
        Products.Add(newProduct);
        return newProduct;
    }

    public bool Delete(int id)
    {
        var product = Products.FirstOrDefault(p => p.Id == id);
        if (product == null)
        {
            return false;
        }

        return Products.Remove(product);
    }

    public int Count()
    {
        return Products.Count;
    }
}
