namespace Guardhouse.SDK.Models.Users;

using System.Text.Json.Serialization;
using NodaTime;

public record GetUserByEmailResponse
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("firstName")]
    public string FirstName { get; init; } = string.Empty;

    [JsonPropertyName("lastName")]
    public string LastName { get; init; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; init; } = string.Empty;

    [JsonPropertyName("roles")]
    public IReadOnlyList<GetUserByEmailResponseRole> Roles { get; init; } = [];

    [JsonPropertyName("permissions")]
    public IReadOnlyList<GetUserByEmailResponsePermission> Permissions { get; init; } = [];

    [JsonPropertyName("lastLogin")]
    [JsonConverter(typeof(InstantJsonConverter))]
    public Instant? LastLogin { get; init; }

    [JsonPropertyName("status")]
    public UserStatus Status { get; init; }

    [JsonPropertyName("isSuspended")]
    public bool IsSuspended { get; init; }

    [JsonPropertyName("isLocked")]
    public bool IsLocked { get; init; }

    [JsonPropertyName("avatarUrl")]
    public string? AvatarUrl { get; init; }
}

public record GetUserByEmailResponseRole
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("key")]
    public string Key { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;
}

public record GetUserByEmailResponsePermission
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("key")]
    public string Key { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;
}
