namespace Guardhouse.SDK.Models;

/// <summary>
/// Defines how client credentials are transmitted to the introspection endpoint.
/// </summary>
public enum IntrospectionCredentialTransmission
{
    /// <summary>
    /// Send credentials using HTTP Basic Authentication in the Authorization header.
    /// Use this when your identity server requires Basic Authentication.
    /// </summary>
    BasicAuth = 0,

    /// <summary>
    /// Send credentials as form data parameters (client_id and client_secret).
    /// This is the SDK default for introspection credential transmission.
    /// </summary>
    FormData = 1
}
