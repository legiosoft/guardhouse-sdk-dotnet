namespace Guardhouse.SDK.Models.Users.Roles;

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Users;

public record GetUserRolesResponse
{
    [JsonPropertyName("roles")]
    public IReadOnlyList<EnumerationModel> Roles { get; init; } = [];
}
