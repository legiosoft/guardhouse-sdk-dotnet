namespace Guardhouse.SDK.Models.Webhooks;

using Enums;

/// <summary>
/// Represents the standard Guardhouse webhook envelope for an event payload.
/// </summary>
/// <typeparam name="T">The payload type carried by the webhook event.</typeparam>
public class WebhookEnvelope<T>
{
    /// <summary>
    /// Gets or sets the type of webhook event.
    /// </summary>
    public WebhookEventType EventType { get; set; }

    /// <summary>
    /// Gets or sets the payload associated with the webhook event.
    /// </summary>
    public T Data { get; set; } = default!;

    /// <summary>
    /// Gets or sets the Unix timestamp, in seconds, when the webhook was produced.
    /// </summary>
    public long Timestamp { get; set; }
}