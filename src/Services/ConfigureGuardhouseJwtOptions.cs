namespace Guardhouse.SDK.Services;

using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Constants;
using Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Models;

internal class ConfigureGuardhouseJwtOptions(
    IOptions<GuardhouseResourceOptions> resourceOptions,
    ILoggerFactory loggerFactory)
    : IConfigureNamedOptions<JwtBearerOptions>
{
    private const string GuardhouseSchemeName = GuardhouseConstants.Authentication.DefaultScheme;

    private readonly IOptions<GuardhouseResourceOptions> _resourceOptions = resourceOptions;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly ILogger _logger = loggerFactory.CreateLogger<ConfigureGuardhouseJwtOptions>();

    public void Configure(JwtBearerOptions options) => Configure(null, options);

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != GuardhouseSchemeName)
        {
            return;
        }

        var opts = _resourceOptions.Value;
        var authorityUri = EnsureAuthority(opts.Authority, opts.RequireHttps);
        var authority = NormalizeAuthority(authorityUri.ToString());

        options.Authority = authority;
        options.Audience = opts.Audience;

        _logger.LogDebugIf(opts.EnableDebug,
            "Configuring JWT Bearer: Authority={Authority}, Audience={Audience}, EnableIntrospection={EnableIntrospection}",
            options.Authority, options.Audience, opts.EnableIntrospection);

        options.RequireHttpsMetadata = opts.RequireHttpsMetadata ?? opts.RequireHttps;
        options.SaveToken = opts.SaveToken;
        options.MapInboundClaims = false;

        ConfigureBackchannel(options, opts, authorityUri);

        options.TokenValidationParameters = BuildTokenValidationParameters(opts, authority);

        _logger.LogDebugIf(opts.EnableDebug,
            "TokenValidationParameters: ValidIssuer={ValidIssuer}, ValidateIssuerSigningKey={ValidateIssuerSigningKey}, ValidAlgorithms={ValidAlgorithms}",
            options.TokenValidationParameters.ValidIssuer,
            options.TokenValidationParameters.ValidateIssuerSigningKey,
            options.TokenValidationParameters.ValidAlgorithms?.Count() > 0
                ? string.Join(", ", options.TokenValidationParameters.ValidAlgorithms)
                : "null");

        if (opts.EnableIntrospection)
        {
            options.EventsType = typeof(GuardhouseIntrospectionJwtBearerEvents);

            options.SecurityTokenValidators.Clear();
            options.SecurityTokenValidators.Add(new GuardhouseOpaqueTokenValidator());
        }
        else
        {
            options.Events = BuildJwtBearerEvents(options);
        }

        options.RefreshOnIssuerKeyNotFound = true;
        options.AutomaticRefreshInterval = TimeSpan.FromMinutes(GetRefreshIntervalMinutes(opts.JwksRefreshIntervalMinutes));
        options.BackchannelTimeout = TimeSpan.FromSeconds(GetRequestTimeoutSeconds(opts.RequestTimeoutSeconds));
    }

    private void ConfigureBackchannel(JwtBearerOptions options, GuardhouseResourceOptions opts, Uri authorityUri)
    {
        var maxRetryAttempts = Math.Max(opts.MaxRetryAttempts, 0);
        var innerHandler = new HttpClientHandler();
        var allowedHosts = BuildAllowedHosts(authorityUri, opts.JwksAllowedHosts);
        var jwksHandler = new GuardhouseJwksHttpHandler(
            innerHandler,
            _loggerFactory.CreateLogger<GuardhouseJwksHttpHandler>(),
            maxRetryAttempts,
            allowedHosts,
            opts.RequireHttps);

        options.BackchannelHttpHandler = jwksHandler;

        _logger.LogDebugIf(opts.EnableDebug, "BackchannelHttpHandler set to: {HandlerType}", jwksHandler.GetType().Name);
        _logger.LogDebugIf(opts.EnableDebug, "Expected JWKS endpoint: {JwksUrl}", BuildJwksUrl(options.Authority));
    }

    private static TokenValidationParameters BuildTokenValidationParameters(GuardhouseResourceOptions opts, string authority)
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = opts.ValidateIssuer,
            ValidIssuer = authority,
            ValidateAudience = opts.ValidateAudience,
            ValidAudience = opts.Audience,
            ValidateLifetime = opts.ValidateLifetime,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.FromMinutes(GuardhouseConstants.Defaults.ClockSkewMinutes),
            RequireSignedTokens = true,
            ValidateIssuerSigningKey = opts.ValidateIssuerSigningKey,
            ValidAlgorithms = NormalizeList(opts.ValidAlgorithms),
            ValidTypes = BuildValidTypes(opts.TokenTypes),
            NameClaimType = "name",
            RoleClaimType = "role"
        };
    }

    private JwtBearerEvents BuildJwtBearerEvents(JwtBearerOptions options)
    {
        return new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                if (context.Exception is SecurityTokenSignatureKeyNotFoundException)
                {
                    LogKeyNotFound(context, options);
                }

                if (context.Exception is SecurityTokenExpiredException)
                {
                    context.Response.Headers["Token-Expired"] = "true";
                }

                return Task.CompletedTask;
            },

            OnTokenValidated = context =>
            {
                LogTokenValidated(context);
                return Task.CompletedTask;
            }
        };
    }

    private void LogKeyNotFound(AuthenticationFailedContext context, JwtBearerOptions options)
    {
        var token = TryReadJwtToken(context.Request);
        if (token == null)
        {
            _logger.LogWarning("JWT signature key not found but token could not be parsed.");
            return;
        }

        _logger.LogError(
            "JWT kid={Kid} not found in JWKS from {Authority}. Token issuer={Issuer}, alg={Alg}",
            token.Header.Kid, options.Authority, token.Issuer, token.Header.Alg);

        _logger.LogDebugIf(_resourceOptions.Value.EnableDebug, "JWKS endpoint: {JwksUrl}", BuildJwksUrl(options.Authority));
        _logger.LogDebugIf(_resourceOptions.Value.EnableDebug, "Ensure JWKS contains kid EXACTLY (case-sensitive): {Kid}", token.Header.Kid);
    }

    private void LogTokenValidated(TokenValidatedContext context)
    {
        var token = TryReadJwtToken(context.SecurityToken, context.Request);
        if (token == null)
        {
            return;
        }

        _logger.LogDebugIf(_resourceOptions.Value.EnableDebug,
            "JWT validated successfully. kid={Kid}, iss={Issuer}, alg={Alg}, exp={Exp}",
            token.Header.Kid, token.Issuer, token.Header.Alg, token.ValidTo);
    }

    private static JwtSecurityToken? TryReadJwtToken(SecurityToken? securityToken, HttpRequest request)
    {
        if (securityToken is JwtSecurityToken jwtToken)
        {
            return jwtToken;
        }

        return TryReadJwtToken(request);
    }

    private static JwtSecurityToken? TryReadJwtToken(HttpRequest request)
    {
        if (!TryGetBearerToken(request, out var token))
        {
            return null;
        }

        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(token))
        {
            return null;
        }

        try
        {
            return handler.ReadJwtToken(token);
        }
        catch
        {
            return null;
        }
    }

    private static bool TryGetBearerToken(HttpRequest request, out string token)
    {
        token = string.Empty;

        if (!request.Headers.TryGetValue(GuardhouseConstants.Headers.Authorization, out var authorization))
        {
            return false;
        }

        var headerValue = authorization.ToString();
        if (!headerValue.StartsWith(GuardhouseConstants.Headers.BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        token = headerValue.Substring(GuardhouseConstants.Headers.BearerPrefix.Length).Trim();
        return !string.IsNullOrEmpty(token);
    }

    private static string NormalizeAuthority(string authority)
    {
        return string.IsNullOrWhiteSpace(authority) ? string.Empty : authority.TrimEnd('/');
    }

    private static Uri EnsureAuthority(string authority, bool requireHttps)
    {
        if (!Uri.TryCreate(authority, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("Authority must be an absolute URI.");
        }

        if (requireHttps && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("HTTPS is required for authority and metadata endpoints.");
        }

        return uri;
    }

    private static HashSet<string> BuildAllowedHosts(Uri authorityUri, string[]? extraHosts)
    {
        var allowedHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            authorityUri.Host
        };

        if (extraHosts == null)
        {
            return allowedHosts;
        }

        foreach (var host in extraHosts)
        {
            if (!string.IsNullOrWhiteSpace(host))
            {
                allowedHosts.Add(host.Trim());
            }
        }

        return allowedHosts;
    }

    private static string BuildJwksUrl(string authority)
    {
        var normalized = NormalizeAuthority(authority);
        return $"{normalized}/{GuardhouseConstants.Endpoints.WellKnownJwks}";
    }

    private static int GetRefreshIntervalMinutes(int refreshIntervalMinutes)
    {
        return refreshIntervalMinutes > 0
            ? refreshIntervalMinutes
            : GuardhouseConstants.Defaults.JwksRefreshIntervalMinutes;
    }

    private static int GetRequestTimeoutSeconds(int requestTimeoutSeconds)
    {
        return requestTimeoutSeconds > 0
            ? requestTimeoutSeconds
            : GuardhouseConstants.Defaults.RequestTimeoutSeconds;
    }

    private static string[] BuildValidTypes(string[]? tokenTypes)
    {
        if (tokenTypes == null || tokenTypes.Length == 0)
        {
            return new[] { GuardhouseConstants.TokenTypes.AtJwt };
        }

        var normalized = new HashSet<string>(StringComparer.Ordinal);
        var allowsJwt = false;

        foreach (var tokenType in tokenTypes)
        {
            var trimmed = tokenType?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                continue;
            }

            if (string.Equals(trimmed, GuardhouseConstants.TokenTypes.Jwt, StringComparison.OrdinalIgnoreCase))
            {
                allowsJwt = true;
            }

            normalized.Add(trimmed);
            normalized.Add(trimmed.ToLowerInvariant());
        }

        if (allowsJwt)
        {
            normalized.Add(GuardhouseConstants.TokenTypes.AtJwt);
        }

        return normalized.Count > 0 ? normalized.ToArray() : new[] { GuardhouseConstants.TokenTypes.AtJwt };
    }

    private static string[]? NormalizeList(string[]? values)
    {
        if (values == null || values.Length == 0)
        {
            return values;
        }

        var normalized = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                normalized.Add(value.Trim().ToUpperInvariant());
            }
        }

        return normalized.Count > 0 ? normalized.ToArray() : Array.Empty<string>();
    }
}

internal sealed class GuardhouseOpaqueTokenValidator : ISecurityTokenValidator
{
    public bool CanValidateToken => true;

    public int MaximumTokenSizeInBytes
    {
        get => TokenValidationParameters.DefaultMaximumTokenSizeInBytes;
        set { }
    }

    public bool CanReadToken(string tokenString) => !string.IsNullOrWhiteSpace(tokenString);

    public ClaimsPrincipal ValidateToken(string tokenString, TokenValidationParameters validationParameters,
        out SecurityToken validatedToken)
    {
        validatedToken = new GuardhouseOpaqueSecurityToken(tokenString);
        return new ClaimsPrincipal(new ClaimsIdentity());
    }
}

internal sealed class GuardhouseOpaqueSecurityToken(string token) : SecurityToken
{
    private readonly string _id = Guid.NewGuid().ToString();

    public string Token { get; } = token;

    public override string Id => _id;

    public override string Issuer => string.Empty;

    public override SecurityKey SecurityKey => null!;

    public override SecurityKey SigningKey { get => null!; set { } }

    public override DateTime ValidFrom => DateTime.MinValue;

    public override DateTime ValidTo => DateTime.MaxValue;
}
