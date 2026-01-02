namespace Guardhouse.SDK.Constants;

public static class GuardhouseConstants
{
    public static class Endpoints
    {
        public const string WellKnownOpenIdConfiguration = ".well-known/openid-configuration";
        public const string WellKnownJwks = ".well-known/jwks.json";
        public const string ConnectToken = "connect/token";
        public const string ConnectIntrospect = "connect/introspect";
        public const string ConnectAuthorize = "connect/authorize";
    }

    public static class Algorithms
    {
        public const string RS256 = "RS256";
        public const string None = "none";
    }

    public static class TokenTypes
    {
        public const string Jwt = "JWT";
    }

    public static class Headers
    {
        public const string Authorization = "Authorization";
        public const string BearerPrefix = "Bearer ";
    }

    public static class JwtClaims
    {
        public const string Type = "typ";
        public const string Algorithm = "alg";
        public const string KeyId = "kid";
        public const string Issuer = "iss";
        public const string Audience = "aud";
        public const string Subject = "sub";
        public const string Expiration = "exp";
        public const string IssuedAt = "iat";
        public const string NotBefore = "nbf";
        public const string JwtId = "jti";
        public const string Scope = "scope";
        public const string ClientId = "client_id";
        public const string TokenType = "token_type";
    }

    public static class Defaults
    {
        public const int JwksCacheDurationHours = 24;
        public const int JwksRefreshIntervalMinutes = 5;
        public const int CacheExpirationBufferSeconds = 60;
        public const int RequestTimeoutSeconds = 30;
        public const int MaxRetryAttempts = 3;
        public const double ClockSkewMinutes = 5.0;
        public const string DefaultScope = "api";
        public const int IntrospectionCacheTtlSeconds = 60;
    }

    public static class Validation
    {
        public const bool ValidateIssuer = true;
        public const bool ValidateAudience = true;
        public const bool ValidateLifetime = true;
        public const bool ValidateIssuerSigningKey = true;
    }
}
