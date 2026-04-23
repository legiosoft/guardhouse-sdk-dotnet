namespace Guardhouse.SDK.Models.Permissions;

using System.Text.Json.Serialization;

public record GetPermissionsResponse
{
    [JsonPropertyName("items")]
    public IReadOnlyList<GetPermissionsResponseItem> Items { get; init; } = [];
}

public record GetPermissionsResponseItem
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("key")]
    public string Key { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("roles")]
    public IReadOnlyList<GetPermissionsResponseRoleItem> Roles { get; init; } = [];
}

public record GetPermissionsResponseRoleItem
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("key")]
    public string Key { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;
}
