namespace Guardhouse.SDK.Models.Permissions;

using System.Text.Json.Serialization;

public record UpdatePermissionRequest
{
    [JsonPropertyName("key")]
    public string Key { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("roleIds")]
    public IReadOnlyList<int> RoleIds { get; init; } = [];
}
