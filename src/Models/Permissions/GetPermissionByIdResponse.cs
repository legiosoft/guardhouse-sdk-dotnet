namespace Guardhouse.SDK.Models.Permissions;

using System.Text.Json.Serialization;

public record GetPermissionByIdResponse
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
