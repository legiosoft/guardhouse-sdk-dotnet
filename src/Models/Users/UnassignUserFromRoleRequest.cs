namespace Guardhouse.SDK.Models.Users;

using System.Text.Json.Serialization;

public record UnassignUserFromRoleRequest
{
    [JsonPropertyName("instantLogout")]
    public bool InstantLogout { get; init; }
}
