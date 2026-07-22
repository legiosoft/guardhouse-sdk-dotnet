using Guardhouse.SDK.Models.Users;
using Guardhouse.SDK.Models.Users.Privacy;
using Guardhouse.SDK.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExampleClient.Controllers;

[ApiController]
[Route("api/guardhouse/users")]
public class UsersController : ControllerBase
{
    private readonly IGuardhouseUsersClient _usersClient;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IGuardhouseUsersClient usersClient, ILogger<UsersController> logger)
    {
        _usersClient = usersClient;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        try
        {
            var createdUser = await _usersClient.CreateUserAsync(request);
            return CreatedAtAction(nameof(GetUserById), new { userId = createdUser.UserId }, createdUser);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to create Guardhouse user");
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating Guardhouse user");
            return StatusCode(500, new { Message = "An unexpected error occurred", Error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<ActionResult> GetUsers([FromQuery] GetUsersRequest request)
    {
        try
        {
            var users = await _usersClient.GetUsersAsync(request);
            return Ok(users);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to get Guardhouse users");
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting Guardhouse users");
            return StatusCode(500, new { Message = "An unexpected error occurred", Error = ex.Message });
        }
    }

    [HttpGet("{userId:int}")]
    public async Task<ActionResult> GetUserById(int userId)
    {
        try
        {
            var user = await _usersClient.GetUserByIdAsync(userId);
            return user is null ? NotFound(new { Message = "User not found" }) : Ok(user);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to get Guardhouse user {UserId}", userId);
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting Guardhouse user {UserId}", userId);
            return StatusCode(500, new { Message = "An unexpected error occurred", Error = ex.Message });
        }
    }

    [HttpPut("{userId:int}")]
    public async Task<ActionResult> UpdateUser(int userId, [FromBody] UpdateUserRequest request)
    {
        try
        {
            var updated = await _usersClient.UpdateUserAsync(userId, request);
            return updated ? NoContent() : NotFound(new { Message = "User not found" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to update Guardhouse user {UserId}", userId);
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating Guardhouse user {UserId}", userId);
            return StatusCode(500, new { Message = "An unexpected error occurred", Error = ex.Message });
        }
    }

    [HttpPost("{userId:int}/password")]
    public async Task<ActionResult> ChangePassword(int userId, [FromBody] ChangePasswordRequest request)
    {
        try
        {
            var changed = await _usersClient.ChangePasswordAsync(userId, request);
            return changed ? NoContent() : NotFound(new { Message = "User not found" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to change Guardhouse user password {UserId}", userId);
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error changing Guardhouse user password {UserId}", userId);
            return StatusCode(500, new { Message = "An unexpected error occurred", Error = ex.Message });
        }
    }

    [HttpPost("{userId:int}/email")]
    public async Task<ActionResult> RequestEmailChange(int userId, [FromBody] RequestEmailChangeRequest request)
    {
        try
        {
            var requested = await _usersClient.RequestEmailChangeAsync(userId, request);
            return requested ? NoContent() : NotFound(new { Message = "User not found" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to request Guardhouse user email change {UserId}", userId);
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error requesting Guardhouse user email change {UserId}", userId);
            return StatusCode(500, new { Message = "An unexpected error occurred", Error = ex.Message });
        }
    }

    [HttpPost("{userId:int}/roles/{roleId:int}")]
    public async Task<ActionResult> AssignRole(int userId, int roleId)
    {
        try
        {
            var assigned = await _usersClient.AssignUserToRoleAsync(userId, roleId);
            return assigned ? NoContent() : NotFound(new { Message = "User not found" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to assign role {RoleId} to Guardhouse user {UserId}", roleId, userId);
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error assigning role {RoleId} to Guardhouse user {UserId}", roleId, userId);
            return StatusCode(500, new { Message = "An unexpected error occurred", Error = ex.Message });
        }
    }

    [HttpDelete("{userId:int}/roles/{roleId:int}")]
    public async Task<ActionResult> UnassignRole(int userId, int roleId, [FromBody] UnassignUserFromRoleRequest request)
    {
        try
        {
            var unassigned = await _usersClient.UnassignUserFromRoleAsync(userId, roleId, request);
            return unassigned ? NoContent() : NotFound(new { Message = "User not found" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to unassign role {RoleId} from Guardhouse user {UserId}", roleId, userId);
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error unassigning role {RoleId} from Guardhouse user {UserId}", roleId, userId);
            return StatusCode(500, new { Message = "An unexpected error occurred", Error = ex.Message });
        }
    }

    [HttpPatch("{userId:int}/block")]
    public async Task<ActionResult> BlockUser(int userId, [FromBody] BlockUserRequest request)
    {
        try
        {
            var blocked = await _usersClient.BlockUserAsync(userId, request);
            return blocked ? NoContent() : NotFound(new { Message = "User not found" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to block Guardhouse user {UserId}", userId);
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error blocking Guardhouse user {UserId}", userId);
            return StatusCode(500, new { Message = "An unexpected error occurred", Error = ex.Message });
        }
    }

    [HttpPatch("{userId:int}/unblock")]
    public async Task<ActionResult> UnblockUser(int userId, [FromBody] UnblockUserRequest request)
    {
        try
        {
            var unblocked = await _usersClient.UnblockUserAsync(userId, request);
            return unblocked ? NoContent() : NotFound(new { Message = "User not found" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to unblock Guardhouse user {UserId}", userId);
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error unblocking Guardhouse user {UserId}", userId);
            return StatusCode(500, new { Message = "An unexpected error occurred", Error = ex.Message });
        }
    }

    [HttpPut("{userId:int}/personal-data")]
    public async Task<ActionResult> DeletePersonalData(int userId, [FromBody] DeleteUserPersonalDataRequest request)
    {
        try
        {
            var deleted = await _usersClient.DeleteUserPersonalDataAsync(userId, request);
            return deleted ? NoContent() : NotFound(new { Message = "User not found" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to delete personal data for Guardhouse user {UserId}", userId);
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting personal data for Guardhouse user {UserId}", userId);
            return StatusCode(500, new { Message = "An unexpected error occurred", Error = ex.Message });
        }
    }
}
