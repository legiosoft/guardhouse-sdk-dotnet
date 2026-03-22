namespace Guardhouse.SDK.Models.Users;

using System.Text.Json.Serialization;

public record GetUserByIdResponse
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("email")]
    public string Email { get; init; } = string.Empty;

    [JsonPropertyName("firstName")]
    public string FirstName { get; init; } = string.Empty;

    [JsonPropertyName("lastName")]
    public string LastName { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public UserStatus Status { get; init; }

    [JsonPropertyName("avatarUrl")]
    public string? AvatarUrl { get; init; }

    [JsonPropertyName("roles")]
    public IReadOnlyList<EnumerationModel> Roles { get; init; } = [];

    [JsonPropertyName("systemPermissions")]
    public IReadOnlyList<EnumerationModel> SystemPermissions { get; init; } = [];
}
