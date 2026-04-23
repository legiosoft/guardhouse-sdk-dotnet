namespace Guardhouse.SDK.Models.Users;

using System.Text.Json.Serialization;

public record CreateUserRequest
{
    [JsonPropertyName("firstName")]
    public string FirstName { get; init; } = string.Empty;

    [JsonPropertyName("lastName")]
    public string LastName { get; init; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; init; } = string.Empty;

    [JsonPropertyName("sendInvite")]
    public bool SendInvite { get; init; }

    [JsonPropertyName("triggerWebhook")]
    public bool TriggerWebhook { get; init; }

    [JsonPropertyName("redirectUrl")]
    public string? RedirectUrl { get; init; }

    [JsonPropertyName("inviterName")]
    public string? InviterName { get; init; }
}
