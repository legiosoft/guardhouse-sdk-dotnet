namespace Guardhouse.SDK.Models.Roles;

using System.Text.Json.Serialization;

public record CreateRoleResponse
{
    [JsonPropertyName("roleId")]
    public int RoleId { get; init; }
}
