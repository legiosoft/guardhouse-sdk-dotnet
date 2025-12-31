# 🚀 Deployment Guide

This document explains how to deploy the Guardhouse SDK for .NET.

## 📋 Prerequisites

1. **.NET 8.0 SDK** - Required for building the project
2. **NuGet API Key** - Required for publishing to NuGet.org
3. **GitHub Repository** - The project is already structured for CI/CD

## 🏗️ Local Development Setup

### Clone the Repository
```bash
git clone https://github.com/your-username/guardhouse-sdk-dotnet.git
cd guardhouse-sdk-dotnet
```

### Restore Dependencies
```bash
dotnet restore
```

### Build the Project
```bash
dotnet build -c Release
```

### Run Tests
```bash
dotnet test --logger "console;verbosity=detailed"
```

### Create Local NuGet Package
```bash
dotnet pack -c Release --output ./artifacts
```

## 🚢 CI/CD Pipeline

The project includes a **GitHub Actions** workflow at `.github/workflows/nuget-publish.yml` that:

### ✅ Automated Features
- **Multi-branch support** (main, develop, tags)
- **Semantic versioning** (automatic version from tags)
- **Comprehensive testing** with coverage reports
- **Artifact management** (test results, coverage, NuGet packages)
- **NuGet publishing** (automatic on tags)
- **Dependency caching** for faster builds

### 🔄 Workflow Triggers
```yaml
on:
  push:
    tags: 'v*'        # Creates releases for version tags
    branches:
      - main          # Continuous integration
      - develop       # Development builds
```

### 📦 Build Process
1. **Checkout** source code
2. **Setup .NET 8.0** environment
3. **Cache NuGet packages** between runs
4. **Restore** dependencies
5. **Determine version** (from git tags/branches)
6. **Build** in Release configuration
7. **Run tests** with detailed logging and coverage
8. **Pack** NuGet package
9. **Upload artifacts** (test results, coverage, package)
10. **Publish to NuGet** (on tags only)

### 🧪 Versioning Strategy
- **Main branch**: `1.0.0` (stable releases)
- **Develop branch**: `1.0.0-preview` (preview releases)
- **Tags**: Extracted from tag name (e.g., `v1.2.3` → `1.2.3`)

## 📦 Package Publishing

### Manual Publishing
```bash
# 1. Login to NuGet
dotnet nuget add source -name "guardhouse-sdk" -src https://api.nuget.org/v3/index.json

# 2. Set API key
dotnet nuget setapikey Your-NuGet-API-Key -source https://api.nuget.org/v3/index.json

# 3. Push package
dotnet nuget push Guardhouse.SDK.1.0.0.nupkg -source https://api.nuget.org/v3/index.json

# 4. Push symbols (optional)
dotnet nuget push Guardhouse.SDK.1.0.0.snupkg -source https://api.nuget.org/v3/index.json
```

### 🏷️ GitHub Actions Publishing
The workflow automatically:
- **Creates release artifacts** on version tags
- **Publishes to NuGet.org** using stored API key
- **Skips duplicate uploads** to prevent errors
- **Supports preview versions** for develop branch

## 🔐 Secrets Configuration

### GitHub Secrets
Set these in your repository settings:

1. `NUGET_API_KEY` - Your NuGet.org API key
2. `NUGET_SOURCE_URL` - (Optional) Custom NuGet feed URL

### Local Development
Create `NuGet.Config` in your user directory:
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="guardhouse-sdk" value="https://api.nuget.org/v3/index.json" />
    <add key="local" value="./packages" />
  </packageSources>
  <packageSourceCredentials>
    <add key="guardhouse-sdk" value="your-api-key-here" />
  </packageSourceCredentials>
</configuration>
```

## 📊 Monitoring and Quality

### Test Coverage
The pipeline collects **code coverage** and uploads it as an artifact. Coverage reports are generated using **XPlat Code Coverage**.

### Artifacts
Each build produces:
- **NuGet package** (`*.nupkg`)
- **Symbol package** (`*.snupkg`)
- **Test results** (`TestResults/`)
- **Coverage report** (`coverage.xml`)

### Quality Gates
Consider adding these quality gates to your workflow:
```yaml
- name: Check code coverage
  run: |
    COVERAGE_THRESHOLD=80
    if [[ $(grep -oP " covered=" TestResults/coverage.xml | awk -F'"' '{print $2}' | sort -nr | tail -1) -lt $COVERAGE_THRESHOLD ]]; then
      echo "Coverage below threshold: ${{ grep -oP " covered=" TestResults/coverage.xml | awk -F'"' '{print $2}' | sort -nr | tail -1 }}%"
      exit 1
    fi
```

## 🎯 Release Process

### Before Release
1. **Ensure all tests pass** in both main and develop
2. **Update version numbers** in `Guardhouse.SDK.csproj` if needed
3. **Update CHANGELOG** with release notes
4. **Create release tag**: `git tag v1.0.0`
5. **Push tag**: `git push origin v1.0.0`

### Automatic Release
- **Push to main** or **create tag** triggers automatic release
- **GitHub Actions** builds, tests, and publishes
- **NuGet package** becomes available within minutes

### Manual Release
```bash
# Create release tag
git tag -a v1.0.0 -m "Release version 1.0.0"
git push origin v1.0.0

# Or use GitHub CLI
gh release create v1.0.0 --generate-notes
```

## 📝 Version Management

### Semantic Versioning
The project follows [Semantic Versioning](https://semver.org/):
- **MAJOR.MINOR.PATCH** (e.g., 1.0.0)
- **PRERELEASE** (e.g., 1.0.0-preview)

### Update Process
1. Update `Guardhouse.SDK.csproj`:
```xml
<PropertyGroup>
    <Version>1.0.1</Version>
    <PackageReleaseNotes>Fix critical bugs and add new features</PackageReleaseNotes>
</PropertyGroup>
```

2. Update version in all files
3. Run full build/test cycle
4. Create git tag and push

## 🔧 Troubleshooting

### Common Issues
```bash
# Build fails with duplicate attributes
rm -rf obj/
dotnet build

# Tests fail with missing dependencies
dotnet clean
dotnet restore
dotnet build

# NuGet push fails
dotnet nuget push --force
```

### Clean Build
```bash
dotnet clean
rm -rf bin/
rm -rf obj/
dotnet restore
dotnet build
```

## 📚 Documentation

### API Documentation
- **XML Documentation**: Generated with XML documentation file
- **README**: Always update with new features
- **CHANGELOG**: Maintain release history

### Code Documentation
- **XML Comments**: All public APIs documented
- **Examples**: Include usage examples
- **Architecture**: Document design decisions

## 🎉 Best Practices

### Pre-commit Hooks
Consider adding these to your project:
```bash
# Install hooks
dotnet tool install --global dotnet-format
dotnet tool install --global dotnet-ef

# Add to .git/hooks/pre-commit
#!/bin/sh
dotnet format --verify-no-changes
dotnet test --no-build
```

### Branch Strategy
- **main**: Stable releases
- **develop**: Integration and feature development
- **feature/*** Feature branches from develop
- **hotfix/*** Emergency fixes from main

### Pull Request Process
1. **Create feature branch** from develop
2. **Implement feature** with tests
3. **Submit PR** to develop
4. **Code review** required
5. **CI/CD** runs on PR
6. **Merge to develop** after approval
7. **Periodic merges** develop → main

## 🔗 Resources

### Documentation
- [NuGet Documentation](https://docs.microsoft.com/en-us/nuget/)
- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [.NET SDK Documentation](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet/)

### Tools
- [NuGet Explorer](https://nuget.org/)
- [Package Explorer](https://packageexplorer.io/)
- [GitHub Releases](https://github.com/guardhouse/guardhouse-sdk-dotnet/releases)

### Support
- **Issues**: [GitHub Issues](https://github.com/guardhouse/guardhouse-sdk-dotnet/issues)
- **Discussions**: [GitHub Discussions](https://github.com/guardhouse/guardhouse-sdk-dotnet/discussions)
- **Email**: support@guardhouse.com

---

*This guide covers all aspects of deploying the Guardhouse SDK. For additional help, please open an issue or contact the support team.*