# Guardhouse SDK System API Guide

This guide documents the Guardhouse SDK surface for the external system API endpoints.

Scope of this guide:

- `Users`
- `Roles`
- `Permissions`

These SDK clients map only to the external endpoints exposed under:

- `GuardHouse/Endpoints/API/Users`
- `GuardHouse/Endpoints/API/Roles`
- `GuardHouse/Endpoints/API/Permissions`

## Overview

The SDK exposes three system API clients:

- `IGuardhouseUsersClient`
- `IGuardhouseRolesClient`
- `IGuardhousePermissionsClient`

For backward compatibility, `IGuardhouseUserService` is still available as an alias of `IGuardhouseUsersClient`.

## Registration

### One-call setup

Use this when the same application both requests machine-to-machine tokens and calls the Guardhouse system API:

```csharp
using Guardhouse.SDK.Constants;
using Guardhouse.SDK.Extensions;

builder.Services.AddGuardhouseClientWithApiClients(
    authority: "https://your-guardhouse-server.com",
    clientId: "your-system-api-client-id",
    clientSecret: "your-system-api-client-secret",
    scope: AuthorizationConsts.Scopes.SystemApi,
    apiBaseUrl: "https://your-guardhouse-server.com");
```

### Split setup

Use this when token configuration is already registered elsewhere:

```csharp
using Guardhouse.SDK.Constants;
using Guardhouse.SDK.Extensions;

builder.Services.AddGuardhouseClient(options =>
{
    options.Authority = "https://your-guardhouse-server.com";
    options.ClientId = "your-system-api-client-id";
    options.ClientSecret = "your-system-api-client-secret";
    options.Scope = AuthorizationConsts.Scopes.SystemApi;
});

builder.Services.AddGuardhouseApiClients(options =>
{
    options.ApiBaseUrl = "https://your-guardhouse-server.com";
});
```

## Configuration Notes

- `Authority` is used by the token client.
- `ApiBaseUrl` is the base URL for the system API endpoints.
- If `ApiBaseUrl` is omitted, the SDK falls back to `Authority`.
- The intended scope for system API access is `AuthorizationConsts.Scopes.SystemApi` (`system_api`).

## Client Injection

Inject the client that matches the endpoint group you need:

```csharp
using Guardhouse.SDK.Services;

public sealed class GuardhouseAdminService(
    IGuardhouseUsersClient usersClient,
    IGuardhouseRolesClient rolesClient,
    IGuardhousePermissionsClient permissionsClient)
{
}
```

## Request Behavior

- Create operations return typed response DTOs.
- Read operations return typed response DTOs, or `null` on `404 Not Found`.
- Update/delete/action operations return `true` on success and `false` on `404 Not Found`.
- Non-success responses other than handled `404` cases throw `InvalidOperationException`.
- Exception messages include sanitized server response summaries rather than raw bodies.

## Users API

### Create user

```csharp
var createdUser = await usersClient.CreateUserAsync(new CreateUserRequest
{
    FirstName = "Ada",
    LastName = "Lovelace",
    Email = "ada@example.com",
    SendInvite = true,
    TriggerWebhook = true,
    RedirectUrl = "https://your-app.example.com/invitation",
    InviterName = "Grace Hopper"
});
```

### Get paged users

```csharp
var users = await usersClient.GetUsersAsync(new GetUsersRequest
{
    PageSize = 25,
    Offset = 0,
    Email = "ada@example.com"
});
```

### Change password

```csharp
var changed = await usersClient.ChangePasswordAsync(42, new ChangePasswordRequest
{
    CurrentPassword = "CurrentPassword123!",
    NewPassword = "NewPassword123!"
});
```

### Endpoint mapping

| Route | Method | SDK method |
|------|--------|------------|
| `api/v1/users` | `POST` | `CreateUserAsync(CreateUserRequest)` |
| `api/v1/users` | `GET` | `GetUsersAsync(GetUsersRequest)` |
| `api/v1/users/{userId}` | `GET` | `GetUserByIdAsync(int)` |
| `api/v1/users/{userId}` | `PUT` | `UpdateUserAsync(int, UpdateUserRequest)` |
| `api/v1/users/{userId}/password` | `POST` | `ChangePasswordAsync(int, ChangePasswordRequest)` |
| `api/v1/users/{userId}/roles/{roleId}` | `POST` | `AssignUserToRoleAsync(int, int)` |
| `api/v1/users/{userId}/roles/{roleId}` | `DELETE` | `UnassignUserFromRoleAsync(int, int, UnassignUserFromRoleRequest)` |
| `api/v1/users/{userId}/block` | `PATCH` | `BlockUserAsync(int, BlockUserRequest)` |
| `api/v1/users/{userId}/unblock` | `PATCH` | `UnblockUserAsync(int, UnblockUserRequest)` |
| `api/v1/users/{userId}/personal-data` | `PUT` | `DeleteUserPersonalDataAsync(int, DeleteUserPersonalDataRequest)` |

## Roles API

### Create role

```csharp
var createdRole = await rolesClient.CreateRoleAsync(new CreateRoleRequest
{
    Key = "system.administrator",
    Name = "System Administrator",
    Description = "Full administrative access"
});
```

### Add permissions to role

```csharp
var updated = await rolesClient.AddPermissionsToRoleAsync(5, new AddPermissionsToRoleRequest
{
    PermissionIds = [10, 11, 12]
});
```

### Endpoint mapping

| Route | Method | SDK method |
|------|--------|------------|
| `api/v1/roles` | `POST` | `CreateRoleAsync(CreateRoleRequest)` |
| `api/v1/roles` | `GET` | `GetRolesAsync()` |
| `api/v1/roles/{roleId}` | `GET` | `GetRoleByIdAsync(int)` |
| `api/v1/roles/{roleId}` | `PUT` | `UpdateRoleAsync(int, UpdateRoleRequest)` |
| `api/v1/roles/{roleId}/permissions` | `POST` | `AddPermissionsToRoleAsync(int, AddPermissionsToRoleRequest)` |
| `api/v1/roles/{roleId}/permissions` | `DELETE` | `RemovePermissionsFromRoleAsync(int, RemovePermissionsFromRoleRequest)` |

## Permissions API

### Create permission

```csharp
var createdPermission = await permissionsClient.CreatePermissionAsync(new CreatePermissionRequest
{
    Key = "business.users.read",
    Name = "Read Users",
    Description = "Allows reading users from the system API"
});
```

### Update permission

```csharp
var updated = await permissionsClient.UpdatePermissionAsync(10, new UpdatePermissionRequest
{
    Key = "business.users.read",
    Name = "Read Users",
    Description = "Allows reading users from the system API",
    RoleIds = [1, 2]
});
```

### Endpoint mapping

| Route | Method | SDK method |
|------|--------|------------|
| `api/v1/permissions` | `POST` | `CreatePermissionAsync(CreatePermissionRequest)` |
| `api/v1/permissions` | `GET` | `GetPermissionsAsync()` |
| `api/v1/permissions/{permissionId}` | `GET` | `GetPermissionByIdAsync(int)` |
| `api/v1/permissions/{permissionId}` | `PUT` | `UpdatePermissionAsync(int, UpdatePermissionRequest)` |

## Models

Main request/response models live under:

- `Guardhouse.SDK.Models.Users`
- `Guardhouse.SDK.Models.Roles`
- `Guardhouse.SDK.Models.Permissions`

The personal-data request model lives under:

- `Guardhouse.SDK.Models.Users.Privacy`

## Example Project

The example application showing these clients is:

- `examples/ExampleClient`

Relevant example controllers:

- `Controllers/UsersController.cs`
- `Controllers/RolesController.cs`
- `Controllers/PermissionsController.cs`

## Important Boundary

This SDK surface intentionally includes only the external system API endpoints documented above.

If a route is not present in the external `Endpoints/API` folders, it should not be treated as part of the SDK system API contract.
