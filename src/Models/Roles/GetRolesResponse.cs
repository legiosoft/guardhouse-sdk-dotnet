namespace Guardhouse.SDK.Models.Roles;

using System.Text.Json.Serialization;

public record GetRolesResponse
{
    [JsonPropertyName("items")]
    public IReadOnlyList<GetRolesResponseItem> Items { get; init; } = [];
}

public record GetRolesResponseItem
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("key")]
    public string Key { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("permissions")]
    public IReadOnlyList<GetRolesResponsePermissionItem> Permissions { get; init; } = [];
}

public record GetRolesResponsePermissionItem
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }
}
