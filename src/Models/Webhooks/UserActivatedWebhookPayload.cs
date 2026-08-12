namespace Guardhouse.SDK.Models.Webhooks;

/// <summary>
/// Represents the payload delivered for a user activated webhook event.
/// </summary>
public record UserActivatedWebhookPayload
{
    /// <summary>
    /// Gets the identifier of the activated user.
    /// </summary>
    public int UserId { get; init; }

    /// <summary>
    /// Gets the email address of the activated user.
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Gets the first name of the activated user.
    /// </summary>
    public string FirstName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the last name of the activated user.
    /// </summary>
    public string LastName { get; init; } = string.Empty;
}
