namespace Guardhouse.SDK.Models.Users.Permissions;

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Users;

public record GetUserPermissionsResponse
{
    [JsonPropertyName("permissions")]
    public IReadOnlyList<EnumerationModel> Permissions { get; init; } = [];
}
