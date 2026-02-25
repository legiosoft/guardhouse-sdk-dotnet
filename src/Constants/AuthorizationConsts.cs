namespace Guardhouse.SDK.Constants;

/// <summary>
/// Authorization-related constants for custom claims, claim values, policies, and scopes.
/// </summary>
public static class AuthorizationConsts
{
    public static class ClaimTypes
    {
        public const string System = "system";
        public const string Business = "business";
    }

    public static class ClaimValues
    {
        public const string SystemAdministrator = "system_administrator";
    }

    public static class Policies
    {
        public const string SA = ClaimTypes.System + "." + ClaimValues.SystemAdministrator;
    }

    public static class Scopes
    {
        public const string SystemApi = "system_api";
    }
}
