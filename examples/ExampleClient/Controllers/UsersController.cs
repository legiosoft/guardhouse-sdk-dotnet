using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExampleClient.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    [HttpGet("profile")]
    [Authorize(Policy = "ReadAccess")]
    public ActionResult GetUserProfile()
    {
        var userId = User.FindFirst("sub")?.Value;
        var name = User.FindFirst("name")?.Value;
        var email = User.FindFirst("email")?.Value;
        var scopes = User.FindAll("scope").Select(c => c.Value).ToList();

        return Ok(new
        {
            UserId = userId,
            Name = name,
            Email = email,
            Scopes = scopes,
            Message = "This endpoint requires 'read' or 'api' scope"
        });
    }

    [HttpGet("all")]
    [Authorize(Policy = "AdminOnly")]
    public ActionResult GetAllUsers()
    {
        var roles = User.FindAll("role").Select(c => c.Value).ToList();
        
        return Ok(new
        {
            Message = "This endpoint is for administrators only",
            AdminUser = User.Identity?.Name,
            Roles = roles,
            Timestamp = DateTime.UtcNow
        });
    }
}
