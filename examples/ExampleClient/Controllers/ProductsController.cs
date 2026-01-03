using ExampleClient.DTOs;
using ExampleClient.Services;
using Guardhouse.SDK.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExampleClient.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly IGuardhouseTokenService _tokenService;

    public ProductsController(IProductService productService, IGuardhouseTokenService tokenService)
    {
        _productService = productService;
        _tokenService = tokenService;
    }

    [HttpGet]
    public async Task<ActionResult> GetAll()
    {
        var accessToken = await _tokenService.GetAccessTokenAsync();
        var products = _productService.GetAll();
        
        return Ok(new
        {
            Products = products,
            AccessTokenPreview = accessToken.Substring(0, Math.Min(20, accessToken.Length)) + "...",
            Message = "Products retrieved successfully"
        });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult> GetById(int id)
    {
        var accessToken = await _tokenService.GetAccessTokenAsync();
        var product = _productService.GetById(id);
        
        if (product == null)
        {
            return NotFound();
        }
        
        return Ok(new
        {
            Product = product,
            AccessTokenPreview = accessToken.Substring(0, Math.Min(20, accessToken.Length)) + "...",
            Message = "Product retrieved successfully"
        });
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] CreateProductRequest request)
    {
        var accessToken = await _tokenService.GetAccessTokenAsync();
        var newProduct = _productService.Create(request);
        
        return CreatedAtAction(nameof(GetById), new { id = newProduct.Id }, new
        {
            Product = newProduct,
            AccessTokenPreview = accessToken.Substring(0, Math.Min(20, accessToken.Length)) + "...",
            Message = "Product created successfully"
        });
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        var accessToken = await _tokenService.GetAccessTokenAsync();
        var product = _productService.GetById(id);
        if (product == null)
        {
            return NotFound();
        }
        
        _productService.Delete(id);
        
        return Ok(new
        {
            Message = $"Product '{product.Name}' deleted successfully",
            AccessTokenPreview = accessToken.Substring(0, Math.Min(20, accessToken.Length)) + "...",
            RemainingProducts = _productService.Count()
        });
    }
}
