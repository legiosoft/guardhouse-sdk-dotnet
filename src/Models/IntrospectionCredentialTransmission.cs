namespace Guardhouse.SDK.Models;

/// <summary>
/// Defines how client credentials are transmitted to the introspection endpoint.
/// </summary>
public enum IntrospectionCredentialTransmission
{
    /// <summary>
    /// Send credentials using HTTP Basic Authentication in the Authorization header.
    /// This is the default and most common approach (RFC 7662 compliant).
    /// </summary>
    BasicAuth = 0,

    /// <summary>
    /// Send credentials as form data parameters (client_id and client_secret).
    /// Use this if your identity server does not support Basic Authentication for introspection.
    /// </summary>
    FormData = 1
}
