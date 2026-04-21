namespace Guardhouse.SDK.Models.Users.Permissions;

using System.Collections.Generic;
using System.Text.Json.Serialization;

public record AddUserPermissionsRequest
{
    [JsonPropertyName("permissionIds")]
    public IReadOnlyList<int> PermissionIds { get; init; } = [];
}
