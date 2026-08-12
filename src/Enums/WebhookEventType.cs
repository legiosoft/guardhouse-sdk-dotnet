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

    /// <summary>
    /// Indicates that an invited user has accepted the invitation and become active.
    /// </summary>
    UserActivated = 3,
}
