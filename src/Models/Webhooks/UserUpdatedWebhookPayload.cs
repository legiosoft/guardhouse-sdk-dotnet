namespace Guardhouse.SDK.Models.Webhooks;

/// <summary>
/// Represents the payload delivered for a user updated webhook event.
/// </summary>
public record UserUpdatedWebhookPayload
{
    /// <summary>
    /// Gets the identifier of the updated user.
    /// </summary>
    public int UserId { get; init; }

    /// <summary>
    /// Gets the email address of the updated user.
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Gets the first name of the updated user.
    /// </summary>
    public string FirstName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the last name of the updated user.
    /// </summary>
    public string LastName { get; init; } = string.Empty;
}