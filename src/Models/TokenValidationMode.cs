namespace Guardhouse.SDK.Models;

/// <summary>
/// Specifies the mode of token validation for resource server authentication.
/// </summary>
public enum TokenValidationMode
{
    /// <summary>
    /// Validates tokens by verifying their JWT signature against the issuer's public keys.
    /// This is the default and most efficient method for stateless token validation.
    /// </summary>
    JwtSignature,

    /// <summary>
    /// Validates tokens by calling the introspection endpoint to check if the token is active.
    /// Use this mode when you need real-time token revocation checking, but note that it adds
    /// a network request per token validation.
    /// </summary>
    Introspection
}
