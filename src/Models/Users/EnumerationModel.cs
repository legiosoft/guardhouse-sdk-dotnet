namespace Guardhouse.SDK.Models.Users;

using System.Text.Json.Serialization;

public record EnumerationModel
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}
