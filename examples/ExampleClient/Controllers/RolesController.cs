using Guardhouse.SDK.Models.Roles;
using Guardhouse.SDK.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExampleClient.Controllers;

[ApiController]
[Route("api/guardhouse/roles")]
public class RolesController : ControllerBase
{
    private readonly IGuardhouseRolesClient _rolesClient;
    private readonly ILogger<RolesController> _logger;

    public RolesController(IGuardhouseRolesClient rolesClient, ILogger<RolesController> logger)
    {
        _rolesClient = rolesClient;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult> CreateRole([FromBody] CreateRoleRequest request)
    {
        try
        {
            var createdRole = await _rolesClient.CreateRoleAsync(request);
            return CreatedAtAction(nameof(GetRoleById), new { roleId = createdRole.RoleId }, createdRole);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to create Guardhouse role");
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating Guardhouse role");
            return StatusCode(500, new { Message = "An unexpected error occurred", Error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<ActionResult> GetRoles()
    {
        try
        {
            var roles = await _rolesClient.GetRolesAsync();
            return Ok(roles);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to get Guardhouse roles");
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting Guardhouse roles");
            return StatusCode(500, new { Message = "An unexpected error occurred", Error = ex.Message });
        }
    }

    [HttpGet("{roleId:int}")]
    public async Task<ActionResult> GetRoleById(int roleId)
    {
        try
        {
            var role = await _rolesClient.GetRoleByIdAsync(roleId);
            return role is null ? NotFound(new { Message = "Role not found" }) : Ok(role);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to get Guardhouse role {RoleId}", roleId);
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting Guardhouse role {RoleId}", roleId);
            return StatusCode(500, new { Message = "An unexpected error occurred", Error = ex.Message });
        }
    }

    [HttpPut("{roleId:int}")]
    public async Task<ActionResult> UpdateRole(int roleId, [FromBody] UpdateRoleRequest request)
    {
        try
        {
            var updated = await _rolesClient.UpdateRoleAsync(roleId, request);
            return updated ? NoContent() : NotFound(new { Message = "Role not found" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to update Guardhouse role {RoleId}", roleId);
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating Guardhouse role {RoleId}", roleId);
            return StatusCode(500, new { Message = "An unexpected error occurred", Error = ex.Message });
        }
    }

    [HttpPost("{roleId:int}/permissions")]
    public async Task<ActionResult> AddPermissions(int roleId, [FromBody] AddPermissionsToRoleRequest request)
    {
        try
        {
            var updated = await _rolesClient.AddPermissionsToRoleAsync(roleId, request);
            return updated ? NoContent() : NotFound(new { Message = "Role not found" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to add permissions to Guardhouse role {RoleId}", roleId);
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error adding permissions to Guardhouse role {RoleId}", roleId);
            return StatusCode(500, new { Message = "An unexpected error occurred", Error = ex.Message });
        }
    }

    [HttpDelete("{roleId:int}/permissions")]
    public async Task<ActionResult> RemovePermissions(int roleId, [FromBody] RemovePermissionsFromRoleRequest request)
    {
        try
        {
            var updated = await _rolesClient.RemovePermissionsFromRoleAsync(roleId, request);
            return updated ? NoContent() : NotFound(new { Message = "Role not found" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to remove permissions from Guardhouse role {RoleId}", roleId);
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error removing permissions from Guardhouse role {RoleId}", roleId);
            return StatusCode(500, new { Message = "An unexpected error occurred", Error = ex.Message });
        }
    }
}
