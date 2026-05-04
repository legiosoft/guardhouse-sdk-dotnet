namespace Guardhouse.SDK.Enums;

/// <summary>
/// Specifies the supported Guardhouse webhook event types.
/// </summary>
public enum WebhookEventType
{
    /// <summary>
    /// Indicates that a new user has been created.
    /// </summary>
    UserCreated = 1,

    /// <summary>
    /// Indicates that an existing user has been updated.
    /// </summary>
    UserUpdated = 2,
}