namespace Guardhouse.SDK.Models.Roles;

using System.Text.Json.Serialization;

public record AddPermissionsToRoleRequest
{
    [JsonPropertyName("permissionIds")]
    public IReadOnlyList<int> PermissionIds { get; init; } = [];
}
