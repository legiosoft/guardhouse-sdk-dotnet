namespace Guardhouse.SDK.Models.Users;

using System.Text.Json.Serialization;

public record CreateUserResponse
{
    [JsonPropertyName("userId")]
    public int UserId { get; init; }
}
