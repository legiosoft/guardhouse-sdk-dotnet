# Deployment Guide

This guide explains how to build, test, pack, and publish `Guardhouse.SDK` to NuGet.org.

## Prerequisites

- .NET SDK capable of building all target frameworks: `net6.0`, `net7.0`, `net8.0`, `net9.0`, and `net10.0`
  - The GitHub workflow installs SDKs `6.0.x` through `10.0.x`.
  - Locally, a current .NET 10 SDK can build this repository when the required targeting packs and package dependencies are restored.
- NuGet.org account with an API key that has `Push` permission for `Guardhouse.SDK`.
- Repository root as the working directory for all commands below.

## Release State

Before publishing `1.0.2`, verify these files are aligned:

- `src/Guardhouse.SDK.csproj`
  - `<Version>1.0.2</Version>`
  - `<PackageId>Guardhouse.SDK</PackageId>`
  - `<PackageReadmeFile>README.md</PackageReadmeFile>`
  - `<PackageLicenseExpression>Apache-2.0</PackageLicenseExpression>`
- `CHANGELOG.md`
  - Has `## [1.0.2] - 2026-04-24`
  - `Unreleased` is empty
- `README.md`
  - Contains NuGet-ready setup examples and current package capabilities
- `docs/SYSTEM_API.md`
  - Documents the users, roles, and permissions system API clients

## Local Build Setup

Use absolute local NuGet paths on Windows. This avoids restore/build mismatches where `project.assets.json` records a relative package folder that MSBuild later resolves from each project directory.

```powershell
$repo = (Resolve-Path '.').Path
$env:DOTNET_CLI_HOME = Join-Path $repo '.dotnet-home'
$env:NUGET_PACKAGES = Join-Path $env:DOTNET_CLI_HOME '.nuget\packages'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
```

You can use the default NuGet cache instead, but keep `DOTNET_CLI_HOME` and `NUGET_PACKAGES` unset or absolute. Avoid setting either one to a relative path.

## Build

```powershell
dotnet restore .\guardhouse-sdk-dotnet.sln
dotnet build .\guardhouse-sdk-dotnet.sln -c Release --no-restore
```

## Test

Run the full test project:

```powershell
dotnet test .\tests\Guardhouse.SDK.Tests\Guardhouse.sdk.tests.csproj -c Release --no-build -v minimal
```

If you need diagnostic output:

```powershell
dotnet test .\tests\Guardhouse.SDK.Tests\Guardhouse.sdk.tests.csproj -c Release --no-build --logger "console;verbosity=detailed"
```

## Pack

Create the NuGet package and symbol package:

```powershell
dotnet pack .\src\Guardhouse.SDK.csproj -c Release --no-build -o .\artifacts -p:Version=1.0.2
```

Expected outputs:

```text
artifacts\Guardhouse.SDK.1.0.2.nupkg
artifacts\Guardhouse.SDK.1.0.2.snupkg
```

Verify the package files exist:

```powershell
Get-ChildItem .\artifacts\Guardhouse.SDK.1.0.2*.nupkg, .\artifacts\Guardhouse.SDK.1.0.2*.snupkg
```

## Publish To NuGet.org

Use an environment variable for the API key so it does not get stored in shell history:

```powershell
$env:NUGET_API_KEY = 'YOUR_NUGET_API_KEY'
```

Push the package:

```powershell
dotnet nuget push .\artifacts\Guardhouse.SDK.1.0.2.nupkg `
  --source https://api.nuget.org/v3/index.json `
  --api-key $env:NUGET_API_KEY `
  --skip-duplicate
```

Push the symbols package:

```powershell
dotnet nuget push .\artifacts\Guardhouse.SDK.1.0.2.snupkg `
  --source https://api.nuget.org/v3/index.json `
  --api-key $env:NUGET_API_KEY `
  --skip-duplicate
```

After publishing, check the package page:

- `https://www.nuget.org/packages/Guardhouse.SDK/1.0.2`

NuGet package indexing can take a few minutes.

## Git Tag

After the package is published and verified, tag the release:

```powershell
git tag -a v1.0.2 -m "Release version 1.0.2"
git push origin v1.0.2
```

## GitHub Actions

The repository currently has `.github/workflows/build.yml`.

It runs on:

- pushes to `dev`
- pull requests targeting `dev`

It performs:

- checkout
- setup .NET SDKs `6.0.x`, `7.0.x`, `8.0.x`, `9.0.x`, and `10.0.x`
- NuGet package cache
- `dotnet restore guardhouse-sdk-dotnet.sln`
- `dotnet build guardhouse-sdk-dotnet.sln --no-restore -c Release`
- `dotnet test guardhouse-sdk-dotnet.sln --no-build -c Release`
- test result artifact upload

It does not publish packages to NuGet.org. Publishing `1.0.2` is a manual step unless a dedicated publish workflow is added later.

## Troubleshooting

### Restore succeeds, build cannot find packages

Use absolute local paths for `DOTNET_CLI_HOME` and `NUGET_PACKAGES`:

```powershell
$repo = (Resolve-Path '.').Path
$env:DOTNET_CLI_HOME = Join-Path $repo '.dotnet-home'
$env:NUGET_PACKAGES = Join-Path $env:DOTNET_CLI_HOME '.nuget\packages'
dotnet restore .\guardhouse-sdk-dotnet.sln
```

Then build again:

```powershell
dotnet build .\guardhouse-sdk-dotnet.sln -c Release --no-restore
```

### NuGet push returns 403 Forbidden

- Verify the API key is active.
- Verify the API key has `Push` permission.
- Verify the API key scope includes package ID `Guardhouse.SDK`.

### NuGet push returns 409 Conflict

The package version already exists on NuGet.org. NuGet packages are immutable, so either:

- keep `--skip-duplicate` when re-running a publish command, or
- increment the package version and rebuild.

### Package validation fails

Check that the package metadata files exist and are included:

- `README.md`
- `LICENSE`
- `icon.png`
- `docs/SYSTEM_API.md` is linked from the README

Then rebuild the package:

```powershell
dotnet pack .\src\Guardhouse.SDK.csproj -c Release -o .\artifacts -p:Version=1.0.2
```

## Pre-Publish Checklist

- [ ] `git status --short` contains only intentional release changes
- [ ] `README.md` is accurate for NuGet consumers
- [ ] `CHANGELOG.md` has the `1.0.2` release section
- [ ] `src/Guardhouse.SDK.csproj` version is `1.0.2`
- [ ] `dotnet build .\guardhouse-sdk-dotnet.sln -c Release --no-restore` passes
- [ ] `dotnet test .\tests\Guardhouse.SDK.Tests\Guardhouse.sdk.tests.csproj -c Release --no-build` passes or known failures are documented
- [ ] `artifacts\Guardhouse.SDK.1.0.2.nupkg` exists
- [ ] `artifacts\Guardhouse.SDK.1.0.2.snupkg` exists
- [ ] Package is visible at `https://www.nuget.org/packages/Guardhouse.SDK/1.0.2`

## Support

- Documentation: https://guardhouse.cloud/docs/getting-started
- GitHub Issues: https://github.com/legiosoft/guardhouse-sdk-dotnet/issues
- Email: support@guardhouse.com

## Additional Resources

- NuGet documentation: https://learn.microsoft.com/nuget/
- GitHub Actions documentation: https://docs.github.com/actions
- .NET SDK documentation: https://learn.microsoft.com/dotnet/
- Semantic Versioning: https://semver.org/
