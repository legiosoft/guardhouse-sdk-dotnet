namespace Guardhouse.SDK.Models.Users.Roles;

using System.Collections.Generic;
using System.Text.Json.Serialization;

public record RemoveUserRolesRequest
{
    [JsonPropertyName("roleIds")]
    public IReadOnlyList<int> RoleIds { get; init; } = [];
}
