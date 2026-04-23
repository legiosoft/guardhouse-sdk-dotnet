# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Comprehensive XML documentation comments for all public APIs
  - `GuardhouseClientOptions` with detailed property descriptions
  - `GuardhouseResourceOptions` with configuration explanations
  - `TokenValidationMode` enum with usage guidance
  - `IntrospectionResponse` and `TokenResponse` models
  - All service interfaces and implementations
  - Extension methods for service registration
  - Custom validation attributes

### Changed
- Made `AddGuardhouseClient(...)`, `AddGuardhouseResource(...)`, and `AddGuardhouseApiClients(...)` idempotent for repeated registration scenarios

### Documentation
- Clarified in the SDK setup docs that repeated Guardhouse DI registration is safe and later configuration delegates still apply

## [1.0.2] - 2026-04-23

### Changed
- Aligned the SDK system API surface with the external `Endpoints/API` contracts for users, roles, and permissions
- Added dedicated `IGuardhouseRolesClient` and `IGuardhousePermissionsClient` typed clients
- Reworked user request and response models to match the external API contracts
- Updated the example client and system API documentation

### Removed
- Removed obsolete SDK-only user roles, user permissions, and user privacy client surfaces that were not backed by external `Endpoints/API` routes

### Documentation
- Refreshed the root `README.md` for NuGet/package consumers
- Added `docs/SYSTEM_API.md` as the dedicated system API usage guide

## [1.0.1] - 2026-03-02

### Fixed
- Fixed introspection validation rejecting active tokens with `Algorithm '' is not allowed` when introspection omitted `alg`
- Added JWT header `alg` fallback when introspection `alg` is missing while preserving allowlist and `none` rejection
- Added regression tests for JWT tokens without introspection `alg` and dotted opaque tokens

## [1.0.0-beta1] - 2026-01-02

### Added
- Initial beta release of Guardhouse SDK for .NET 8.0
- **JWT Signature Validation** (RFC 7515)
  - JWKS endpoint integration for token validation
  - Automatic JWKS caching with configurable duration (default: 24 hours)
  - Lazy refresh on unknown keys with rate limiting (default: 5 minutes)
  - Support for `/.well-known/openid-configuration` and `/.well-known/jwks.json` endpoints
  
- **Token Introspection** (RFC 7662)
  - Introspection endpoint integration for token validation
  - Client credentials support for introspection calls
  - Caching of introspection results
  
- **Security Validations**
  - Strict RS256 algorithm enforcement (prevents algorithm confusion attacks)
  - Token type (`typ`) header validation
  - Issuer validation
  - Audience validation
  - Lifetime validation
  - Signature validation
  - Embedded key attack prevention
  - Key ID injection (path traversal) prevention
  - Psychic signature protection
  - None algorithm prevention
  - Confused deputy attack prevention
  
- **Configuration Options**
  - `GuardhouseClientOptions` - Client credentials configuration
  - `GuardhouseResourceOptions` - Resource server configuration
  - `TokenValidationMode` - Switch between JWT signature and Introspection modes
  - Configurable cache durations, timeouts, and retry policies
  - Strong validation at startup for required configuration
  
- **Resilience & Performance**
  - HTTP resilience with Polly (retry, timeout, circuit breaker)
  - Token caching with configurable expiration buffer
  - Automatic token refresh using refresh tokens
  - Backchannel timeout and retry policies
  
- **Developer Experience**
  - Extension methods for easy service registration
  - Clear error messages at startup for misconfiguration
  - Comprehensive XML documentation
  - Constants for all sensitive strings, algorithms, and endpoints
  - NodaTime integration for precise time handling

### Security
- All 10 JWT security attack vectors addressed
- Configuration validation prevents runtime errors
- No fallback between validation methods
- Proper HTTPS metadata enforcement

### Dependencies
- .NET 6.0, 7.0, 8.0, 9.0, 10.0
- Microsoft.Extensions.* (DI, HTTP, Options, Caching, Authentication)
- System.IdentityModel.Tokens.Jwt
- NodaTime
- Polly

### Known Limitations (Beta)
- Strong name signing not yet implemented
- Limited to client credentials flow
- No support for PKCE or authorization code flow

### Breaking Changes (for future GA)
- None in this beta

### Migration Guide
N/A - Initial release

---

## [0.0.0] - Unreleased

[Future GA Release]
