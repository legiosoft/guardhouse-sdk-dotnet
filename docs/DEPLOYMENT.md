# Deployment Guide

This guide explains how to build, test, and publish the Guardhouse SDK for .NET to NuGet.org.

## Prerequisites

- **.NET 8.0 SDK** or later (for building)
- **NuGet.org Account** with API key (for publishing)
- **GitHub Repository** with workflow configured

## Local Development

### Clone Repository

```bash
git clone https://github.com/legiosoft/guardhouse-sdk-dotnet.git
cd guardhouse-sdk-dotnet
```

### Build Project

```bash
dotnet restore
dotnet build -c Release
```

### Run Tests

```bash
dotnet test -c Release --logger "console;verbosity=detailed"
```

### Create Local NuGet Package

```bash
dotnet pack -c Release -o ./artifacts
```

The packages will be created in the `./artifacts` directory.

## Version Management

### Semantic Versioning

The project follows [Semantic Versioning 2.0.0](https://semver.org/):

- **Format**: `MAJOR.MINOR.PATCH` (e.g., `1.0.0`)
- **Prerelease**: `MAJOR.MINOR.PATCH-prerelease` (e.g., `1.0.0-beta1`)
- **Build Metadata**: `MAJOR.MINOR.PATCH+build` (e.g., `1.0.0+20250102`)

### Update Version in Project File

Edit `src/Guardhouse.SDK.csproj`:

```xml
<PropertyGroup>
    <Version>1.0.0</Version>
    <!-- Or for prerelease -->
    <Version>1.0.0-beta1</Version>
</PropertyGroup>
```

### Version Sources

1. **Project File**: Used for local builds and CI builds
2. **Git Tags**: Used for release builds (e.g., `v1.0.0` → version `1.0.0`)
3. **CI Tags**: Generated automatically for PR builds (`1.0.0-ci`)

## Publishing to NuGet.org

### Manual Publishing

#### 1. Create API Key

1. Go to [NuGet.org](https://www.nuget.org/)
2. Sign in and navigate to **API Keys**
3. Create a new key with **Push** scope for the package `Guardhouse.SDK`

#### 2. Push Package

```bash
# Push all packages from artifacts directory
dotnet nuget push ./artifacts/*.nupkg --source https://api.nuget.org/v3/index.json --api-key YOUR_API_KEY


# Push specific package
dotnet nuget push ./artifacts/Guardhouse.SDK.1.0.0.nupkg --source https://api.nuget.org/v3/index.json --api-key YOUR_API_KEY
```

#### 3. Push Symbols (Optional)

```bash
dotnet nuget push ./artifacts/*.snupkg --source https://api.nuget.org/v3/index.json --api-key YOUR_API_KEY
```

### Automatic Publishing via GitHub Actions

The workflow at `.github/workflows/nuget-publish.yml` automates the entire process.

#### Workflow Triggers

- **Version tags**: `v1.0.0`, `v1.0.0-beta1`, etc. → Builds and publishes to NuGet.org
- **Main branch pushes**: Builds and tests only (no publishing)
- **Pull requests**: Builds and tests only (no publishing)

#### Setup GitHub Secrets

1. Go to your repository **Settings** → **Secrets and variables** → **Actions**
2. Add the following secret:

   **`NUGET_API_KEY`**: Your NuGet.org API key

#### Create Release Tag

```bash
# Create and push version tag
git tag v1.0.0
git push origin v1.0.0

# Or with annotation
git tag -a v1.0.0 -m "Release version 1.0.0"
git push origin v1.0.0
```

The GitHub Actions workflow will:
1. Extract version from tag (`v1.0.0` → `1.0.0`)
2. Build the project with the extracted version
3. Run all tests
4. Create NuGet package
5. Publish to NuGet.org
6. Create GitHub release

#### Check Workflow Status

1. Go to **Actions** tab in your GitHub repository
2. Click on the workflow run
3. View logs for each step

## Workflow Details

### Build Job

```yaml
- Checkout source code
- Setup .NET 8.0 SDK
- Cache NuGet packages (speeds up subsequent builds)
- Determine version from git tag or project file
- Restore dependencies
- Build in Release configuration
- Run all tests
- Upload test results as artifacts
- Pack NuGet packages (on push only)
- Upload packages as artifacts (on push only)
```

### Publish Job

```yaml
- Download NuGet packages from artifacts
- Download symbol packages from artifacts
- Verify packages exist
- Push to NuGet.org using API key
- Push symbols to NuGet.org
- Create GitHub release
```

### Artifacts

Each build produces:

- **Test Results**: `test-results.zip` (30-day retention)
- **NuGet Packages**: `nuget-packages.zip` (90-day retention)
- **Symbol Packages**: `symbol-packages.zip` (90-day retention)

## Troubleshooting

### Build Errors

**Error: "Duplicate attributes"**

```bash
dotnet clean
rm -rf bin/ obj/
dotnet restore
dotnet build
```

**Error: "Missing dependencies"**

```bash
dotnet clean
dotnet restore --force-evaluate
dotnet build
```

### Test Failures

**Tests fail unexpectedly**

```bash
# Run tests with detailed output
dotnet test -c Release --logger "console;verbosity=detailed"

# Run specific test
dotnet test -c Release --filter "FullyQualifiedName~TestName"
```

### Publishing Errors

**Error: "403 Forbidden"**

- Verify your NuGet API key is valid
- Ensure the key has **Push** permissions
- Check the package name matches your API key scope

**Error: "409 Conflict - Package already exists"**

- Package version already published
- Use `--skip-duplicate` flag to ignore
- Increment version number in project file

**Error: "Package validation failed"**

- Ensure package metadata is complete
- Verify icon file exists (`icon.png`)
- Check README.md is included

### Workflow Failures

**Workflow doesn't trigger**

- Verify tag format: `v*` (e.g., `v1.0.0`, not `1.0.0`)
- Check branch protection rules
- Ensure GitHub Actions is enabled for repository

**NuGet push fails**

- Verify `NUGET_API_KEY` secret is set correctly
- Check the key hasn't expired
- Ensure NuGet.org is accessible

## Best Practices

### Pre-Release Checklist

- [ ] All tests pass locally
- [ ] Documentation is updated
- [ ] CHANGELOG.md is updated
- [ ] Version number is incremented
- [ ] Release notes are prepared

### Release Process

1. **Update version** in `Guardhouse.SDK.csproj`
2. **Update CHANGELOG.md** with release notes
3. **Commit changes**: `git commit -am "Release version X.Y.Z"`
4. **Create tag**: `git tag vX.Y.Z`
5. **Push tag**: `git push origin vX.Y.Z`
6. **Monitor workflow** in GitHub Actions
7. **Verify package** on NuGet.org

### Branch Strategy

- **main**: Production-ready code
- **develop**: Integration and feature development
- **feature/***: Feature branches from develop
- **hotfix/***: Emergency fixes from main

### Quality Gates

Ensure before pushing:

```bash
# Format code
dotnet format --verify-no-changes

# Run linters
dotnet build -c Release

# Run tests
dotnet test -c Release

# Pack to verify
dotnet pack -c Release -o ./test-pack
```

## Configuration

### NuGet.Config (Optional)

Create `NuGet.Config` in repository root for local development:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="local" value="./packages" />
  </packageSources>
</configuration>
```

### Global Settings

```bash
# Set default API key
dotnet nuget setapikey YOUR_API_KEY

# List configured sources
dotnet nuget sources list

# Add custom source
dotnet nuget add source https://custom-feed.com/index.json -n custom
```

## Monitoring

### Package Statistics

- **NuGet.org**: Visit package page for download stats
- **GitHub Releases**: Check release artifacts
- **GitHub Actions**: View workflow runs and logs

### Test Results

- Download test results artifacts from GitHub Actions
- View test logs for debugging

## Support

- **Documentation**: https://docs.guardhouse.cloud
- **GitHub Issues**: https://github.com/legiosoft/guardhouse-sdk-dotnet/issues
- **Email**: support@guardhouse.com

## Additional Resources

- [NuGet Documentation](https://docs.microsoft.com/nuget/)
- [GitHub Actions Documentation](https://docs.github.com/actions)
- [.NET SDK Documentation](https://docs.microsoft.com/dotnet/)
- [Semantic Versioning](https://semver.org/)
