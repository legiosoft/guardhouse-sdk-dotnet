using ExampleResource.DTOs;
using ExampleResource.Models;
using ExampleResource.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExampleResource.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    public ActionResult<IEnumerable<Product>> GetAll()
    {
        return Ok(new
        {
            Products = _productService.GetAll(),
            Message = "Products retrieved successfully (public endpoint)"
        });
    }

    [HttpGet("{id}")]
    [Authorize]
    public ActionResult<Product> GetById(int id)
    {
        var product = _productService.GetById(id);
        
        if (product == null)
        {
            return NotFound(new { Message = $"Product with id {id} not found" });
        }
        
        return Ok(new
        {
            Product = product,
            User = User.Identity?.Name,
            Subject = User.FindFirst("sub")?.Value,
            Scopes = User.FindAll("scope").Select(c => c.Value).ToList(),
            Message = "Product retrieved successfully (protected endpoint)"
        });
    }

    [HttpPost]
    [Authorize(Policy = "WriteScope")]
    public ActionResult<Product> Create([FromBody] CreateProductRequest request)
    {
        var newProduct = _productService.Create(request);
        
        return CreatedAtAction(nameof(GetById), new { id = newProduct.Id }, new
        {
            Product = newProduct,
            User = User.Identity?.Name,
            Subject = User.FindFirst("sub")?.Value,
            Scopes = User.FindAll("scope").Select(c => c.Value).ToList(),
            Message = "Product created successfully (protected endpoint)"
        });
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminRole")]
    public ActionResult Delete(int id)
    {
        var product = _productService.GetById(id);
        if (product == null)
        {
            return NotFound(new { Message = $"Product with id {id} not found" });
        }
        
        _productService.Delete(id);
        
        return Ok(new
        {
            Message = $"Product '{product.Name}' deleted successfully (admin only)",
            User = User.Identity?.Name,
            Subject = User.FindFirst("sub")?.Value,
            Roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList(),
            RemainingProducts = _productService.Count()
        });
    }
}
