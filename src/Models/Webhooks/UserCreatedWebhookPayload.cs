namespace Guardhouse.SDK.Models.Webhooks;

/// <summary>
/// Represents the payload delivered for a user created webhook event.
/// </summary>
public class UserCreatedWebhookPayload
{
    /// <summary>
    /// Gets the identifier of the created user.
    /// </summary>
    public int UserId { get; init; }

    /// <summary>
    /// Gets the email address of the created user.
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Gets the first name of the created user.
    /// </summary>
    public string FirstName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the last name of the created user.
    /// </summary>
    public string LastName { get; init; } = string.Empty;
}