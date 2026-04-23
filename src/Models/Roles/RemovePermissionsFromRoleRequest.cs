namespace Guardhouse.SDK.Models.Roles;

using System.Text.Json.Serialization;

public record RemovePermissionsFromRoleRequest
{
    [JsonPropertyName("permissionIds")]
    public IReadOnlyList<int> PermissionIds { get; init; } = [];
}
