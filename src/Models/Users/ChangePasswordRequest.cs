namespace Guardhouse.SDK.Models.Users;

using System.Text.Json.Serialization;

public record ChangePasswordRequest
{
    [JsonPropertyName("currentPassword")]
    public string? CurrentPassword { get; init; }

    [JsonPropertyName("newPassword")]
    public string NewPassword { get; init; } = string.Empty;
}
