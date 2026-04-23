using Guardhouse.SDK.Models.Permissions;
using Guardhouse.SDK.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExampleClient.Controllers;

[ApiController]
[Route("api/guardhouse/permissions")]
public class PermissionsController : ControllerBase
{
    private readonly IGuardhousePermissionsClient _permissionsClient;
    private readonly ILogger<PermissionsController> _logger;

    public PermissionsController(IGuardhousePermissionsClient permissionsClient, ILogger<PermissionsController> logger)
    {
        _permissionsClient = permissionsClient;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult> CreatePermission([FromBody] CreatePermissionRequest request)
    {
        try
        {
            var createdPermission = await _permissionsClient.CreatePermissionAsync(request);
            return CreatedAtAction(nameof(GetPermissionById), new { permissionId = createdPermission.Id }, createdPermission);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to create Guardhouse permission");
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating Guardhouse permission");
            return StatusCode(500, new { Message = "An unexpected error occurred", Error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<ActionResult> GetPermissions()
    {
        try
        {
            var permissions = await _permissionsClient.GetPermissionsAsync();
            return Ok(permissions);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to get Guardhouse permissions");
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting Guardhouse permissions");
            return StatusCode(500, new { Message = "An unexpected error occurred", Error = ex.Message });
        }
    }

    [HttpGet("{permissionId:int}")]
    public async Task<ActionResult> GetPermissionById(int permissionId)
    {
        try
        {
            var permission = await _permissionsClient.GetPermissionByIdAsync(permissionId);
            return permission is null ? NotFound(new { Message = "Permission not found" }) : Ok(permission);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to get Guardhouse permission {PermissionId}", permissionId);
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting Guardhouse permission {PermissionId}", permissionId);
            return StatusCode(500, new { Message = "An unexpected error occurred", Error = ex.Message });
        }
    }

    [HttpPut("{permissionId:int}")]
    public async Task<ActionResult> UpdatePermission(int permissionId, [FromBody] UpdatePermissionRequest request)
    {
        try
        {
            var updated = await _permissionsClient.UpdatePermissionAsync(permissionId, request);
            return updated ? NoContent() : NotFound(new { Message = "Permission not found" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to update Guardhouse permission {PermissionId}", permissionId);
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating Guardhouse permission {PermissionId}", permissionId);
            return StatusCode(500, new { Message = "An unexpected error occurred", Error = ex.Message });
        }
    }
}
