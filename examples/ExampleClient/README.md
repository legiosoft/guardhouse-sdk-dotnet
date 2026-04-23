# Example Client Application

This example shows how to use `Guardhouse.SDK` as a machine-to-machine client that:

- requests access tokens
- introspects tokens
- calls the external Guardhouse system API for users, roles, and permissions

## Configuration

Update [appsettings.json](/C:/Sourse/Common/GuardHouse/guardhouse-sdk-dotnet/examples/ExampleClient/appsettings.json) with real Guardhouse values before running:

```json
{
  "Guardhouse": {
    "Authority": "https://your-guardhouse-server.com",
    "ApiBaseUrl": "https://your-guardhouse-server.com",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret",
    "Scope": "system_api"
  }
}
```

Notes:

- `Authority` is used for token acquisition and introspection.
- `ApiBaseUrl` is used by the system API clients. In most deployments it is the same as `Authority`.
- `Scope` should be `system_api` for the external Guardhouse system API.

## What It Exposes

Token endpoints:

- `GET /api/token/current`
- `GET /api/token/refresh`
- `POST /api/token/introspect`
- `GET /api/token/check-active`

Guardhouse system API examples:

- `POST /api/guardhouse/users`
- `GET /api/guardhouse/users`
- `GET /api/guardhouse/users/{userId}`
- `PUT /api/guardhouse/users/{userId}`
- `POST /api/guardhouse/users/{userId}/password`
- `POST /api/guardhouse/users/{userId}/roles/{roleId}`
- `DELETE /api/guardhouse/users/{userId}/roles/{roleId}`
- `PATCH /api/guardhouse/users/{userId}/block`
- `PATCH /api/guardhouse/users/{userId}/unblock`
- `PUT /api/guardhouse/users/{userId}/personal-data`
- `POST /api/guardhouse/roles`
- `GET /api/guardhouse/roles`
- `GET /api/guardhouse/roles/{roleId}`
- `PUT /api/guardhouse/roles/{roleId}`
- `POST /api/guardhouse/roles/{roleId}/permissions`
- `DELETE /api/guardhouse/roles/{roleId}/permissions`
- `POST /api/guardhouse/permissions`
- `GET /api/guardhouse/permissions`
- `GET /api/guardhouse/permissions/{permissionId}`
- `PUT /api/guardhouse/permissions/{permissionId}`

The example also includes `ProductsController` as a simple token-consumer sample for calling another protected API.

## Run

```bash
cd examples/ExampleClient
dotnet run
```

Default URLs:

- `https://localhost:5001/swagger`
- `http://localhost:5000`

## Implementation Notes

- Token client registration is in [Program.cs](/C:/Sourse/Common/GuardHouse/guardhouse-sdk-dotnet/examples/ExampleClient/Program.cs).
- System API controller examples are in:
  - [UsersController.cs](/C:/Sourse/Common/GuardHouse/guardhouse-sdk-dotnet/examples/ExampleClient/Controllers/UsersController.cs)
  - [RolesController.cs](/C:/Sourse/Common/GuardHouse/guardhouse-sdk-dotnet/examples/ExampleClient/Controllers/RolesController.cs)
  - [PermissionsController.cs](/C:/Sourse/Common/GuardHouse/guardhouse-sdk-dotnet/examples/ExampleClient/Controllers/PermissionsController.cs)

For the SDK surface itself, see [docs/SYSTEM_API.md](/C:/Sourse/Common/GuardHouse/guardhouse-sdk-dotnet/docs/SYSTEM_API.md).
