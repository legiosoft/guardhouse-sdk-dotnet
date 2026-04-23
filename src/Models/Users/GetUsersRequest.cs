namespace Guardhouse.SDK.Models.Users;

using System.Text.Json.Serialization;

public record GetUsersRequest
{
    [JsonPropertyName("pageSize")]
    public int PageSize { get; init; }

    [JsonPropertyName("offset")]
    public int Offset { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }
}
