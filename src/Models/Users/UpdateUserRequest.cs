namespace Guardhouse.SDK.Models.Users;

using System.Text.Json.Serialization;

public record UpdateUserRequest
{
    [JsonPropertyName("firstName")]
    public string FirstName { get; init; } = string.Empty;

    [JsonPropertyName("lastName")]
    public string LastName { get; init; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; init; } = string.Empty;
}
