namespace Guardhouse.SDK.Models.Users.Roles;

using System.Collections.Generic;
using System.Text.Json.Serialization;

public record SetUserRolesRequest
{
    [JsonPropertyName("roleIds")]
    public IReadOnlyList<int> RoleIds { get; init; } = [];
}
