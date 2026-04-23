namespace Guardhouse.SDK.Models.Users;

using System.Text.Json.Serialization;

public record BlockUserRequest
{
    [JsonPropertyName("triggerWebhook")]
    public bool TriggerWebhook { get; init; }

    [JsonPropertyName("notifyUserViaEmail")]
    public bool NotifyUserViaEmail { get; init; }

    [JsonPropertyName("suspensionReason")]
    public string SuspensionReason { get; init; } = string.Empty;

    [JsonPropertyName("blockedByUserId")]
    public int BlockedByUserId { get; init; }
}
