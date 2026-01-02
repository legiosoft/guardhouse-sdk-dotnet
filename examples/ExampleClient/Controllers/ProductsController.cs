using ExampleClient.DTOs;
using ExampleClient.Models;
using ExampleClient.Services;
using Guardhouse.SDK.Services;
using Microsoft.AspNetCore.Authorization;
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
    public ActionResult<IEnumerable<Product>> GetAll()
    {
        return Ok(_productService.GetAll());
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<Product>> GetById(int id)
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
            AccessToken = accessToken.Substring(0, Math.Min(20, accessToken.Length)) + "...",
            Message = "Product retrieved successfully"
        });
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Product>> Create([FromBody] CreateProductRequest request)
    {
        var newProduct = _productService.Create(request);
        var accessToken = await _tokenService.GetAccessTokenAsync();
        
        return CreatedAtAction(nameof(GetById), new { id = newProduct.Id }, new
        {
            Product = newProduct,
            AccessToken = accessToken.Substring(0, Math.Min(20, accessToken.Length)) + "...",
            Message = "Product created successfully"
        });
    }

    [HttpDelete("{id}")]
    [Authorize]
    public ActionResult Delete(int id)
    {
        var product = _productService.GetById(id);
        if (product == null)
        {
            return NotFound();
        }
        
        _productService.Delete(id);
        
        return Ok(new
        {
            Message = $"Product '{product.Name}' deleted successfully",
            RemainingProducts = _productService.Count()
        });
    }
}
