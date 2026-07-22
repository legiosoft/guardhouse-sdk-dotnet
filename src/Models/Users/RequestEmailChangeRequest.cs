namespace Guardhouse.SDK.Models.Users;

using System.Text.Json.Serialization;

public record RequestEmailChangeRequest
{
    [JsonPropertyName("newEmail")]
    public string NewEmail { get; init; } = string.Empty;
}
