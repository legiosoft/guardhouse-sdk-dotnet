namespace Guardhouse.SDK.Models.Permissions;

using System.Text.Json.Serialization;

public record CreatePermissionResponse
{
    [JsonPropertyName("id")]
    public int Id { get; init; }
}
