using ExampleClient.DTOs;
using ExampleClient.Services;
using Guardhouse.SDK.Extensions;
using Guardhouse.SDK.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExampleClient.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly IGuardhouseTokenService _tokenService;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(IProductService productService, IGuardhouseTokenService tokenService, ILogger<ProductsController> logger)
    {
        _productService = productService;
        _tokenService = tokenService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult> GetAll()
    {
        try
        {
            var accessToken = await _tokenService.GetAccessTokenAsync();
            var products = _productService.GetAll();

            return Ok(new
            {
                Products = products,
                AccessTokenPreview = accessToken.GetPreview(20),
                Message = "Products retrieved successfully"
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to get access token");
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting products");
            return StatusCode(500, new { Message = "An unexpected error occurred", Error = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult> GetById(int id)
    {
        try
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
                AccessTokenPreview = accessToken.GetPreview(20),
                Message = "Product retrieved successfully"
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to get access token");
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting product");
            return StatusCode(500, new { Message = "An unexpected error occurred", Error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] CreateProductRequest request)
    {
        try
        {
            var accessToken = await _tokenService.GetAccessTokenAsync();
            var newProduct = _productService.Create(request);

            return CreatedAtAction(nameof(GetById), new { id = newProduct.Id }, new
            {
                Product = newProduct,
                AccessTokenPreview = accessToken.GetPreview(20),
                Message = "Product created successfully"
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to get access token");
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating product");
            return StatusCode(500, new { Message = "An unexpected error occurred", Error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        try
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
                AccessTokenPreview = accessToken.GetPreview(20),
                RemainingProducts = _productService.Count()
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to get access token");
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting product");
            return StatusCode(500, new { Message = "An unexpected error occurred", Error = ex.Message });
        }
    }
}
