namespace Guardhouse.SDK.Models.Users;

using System.Text.Json.Serialization;

public record UnblockUserRequest
{
    [JsonPropertyName("triggerWebhook")]
    public bool TriggerWebhook { get; init; }

    [JsonPropertyName("notifyUserViaEmail")]
    public bool NotifyUserViaEmail { get; init; }

    [JsonPropertyName("unblockedByUserId")]
    public int UnblockedByUserId { get; init; }
}
