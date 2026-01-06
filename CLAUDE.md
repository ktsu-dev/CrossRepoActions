# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

CrossRepoActions is a .NET 9.0 console application that automates operations across multiple Git repositories. It provides a menu-driven interface for discovering, building, testing, and updating packages across a collection of repositories (typically in c:/dev/ktsu-dev by default).

## Build and Test Commands

### Building
```powershell
dotnet build --nologo
```

### Running Tests
```powershell
dotnet test --nologo
```

### Running the Application
```powershell
dotnet run
# Or with custom path:
dotnet run -- -p "c:/your/custom/path"
```

### CI/CD Pipeline (used by GitHub Actions)
```powershell
Import-Module ./scripts/PSBuild.psm1
$buildConfig = Get-BuildConfiguration [params...]
Invoke-CIPipeline -BuildConfiguration $buildConfig.Data
```

## Architecture

### Command Pattern with Verbs
The application uses CommandLineParser with a verb-based command pattern. Each verb represents a distinct action:

- **Menu** (default): Interactive menu for selecting verbs
- **DiscoverRepositories**: Scans for Git repositories under a root path
- **DiscoverSolutions**: Finds .NET solutions within discovered repositories
- **BuildAndTest**: Builds and tests all discovered solutions in parallel
- **UpdatePackages**: Monitors and auto-updates NuGet packages across solutions
- **GitPull**: Pulls all repositories

All verbs inherit from `BaseVerb` or `BaseVerb<T>` in CrossRepoActions/Verbs/. New verbs are automatically discovered via reflection using the `[Verb]` attribute.

### Persistent State
Uses ktsu.AppDataStorage to cache discovered repositories and solutions in `PersistentState.cs`. This cache is cleared when running DiscoverRepositories explicitly.

### PowerShell Integration
The application heavily uses PowerShell Core (Microsoft.PowerShell.SDK) for:
- Git operations (pull, push, status, commit) via `Git.cs`
- .NET CLI commands (build, test, package updates) via `Dotnet.cs`
- Extension method `InvokeAndReturnOutput()` in `PowershellExtensions.cs` simplifies PowerShell execution

### CI/CD with PSBuild Module
The `scripts/PSBuild.psm1` module provides a complete CI/CD pipeline:
- Git-based versioning from tags and changelog
- Build, test, package, and release automation
- SonarQube integration
- NuGet and GitHub package publishing
- Release creation with assets

Called by `.github/workflows/dotnet.yml` which runs on push to main/develop and nightly.

### Key Dependencies
- **CommandLineParser**: Verb-based CLI parsing
- **ConsoleTools**: Menu UI components
- **Microsoft.PowerShell.SDK**: PowerShell automation
- **ktsu.AppDataStorage**: Persistent state management
- **ktsu.RunCommand**: Command execution utilities
- **ktsu.StrongPaths**: Type-safe path handling
- **NuGet.Versioning**: Package version comparison

### Solution Discovery and Package Management
`Dotnet.cs` contains logic for:
- Discovering solutions and projects using `dotnet sln list`
- Parsing package references from project files
- Checking for outdated packages via `dotnet list package --outdated`
- Updating packages via `dotnet add package`

The UpdatePackages verb runs continuously (5-minute intervals) and auto-commits package updates to Git if the project file was not already modified.

## Project Configuration

- **SDK**: .NET 9.0 with custom ktsu.Sdk.App SDK (version 1.8.0)
- **Central Package Management**: Enabled via Directory.Packages.props
- **Global SDK Settings**: global.json pins SDK version and MSBuild SDKs
- **Solution File**: CrossRepoActions.sln at repository root

## Typical Workflow

1. Run without arguments to launch interactive menu
2. First use: Select "DiscoverRepositories" to scan for repos
3. Select "BuildAndTest" to verify all solutions compile
4. Select "UpdatePackages" to continuously monitor and update dependencies
5. Use "GitPull" to sync all repositories

## Notes

- Default root path is "c:/dev/ktsu-dev" (configurable with `-p` argument)
- Maximum parallelism is unbounded (Program.MaxParallelism = -1)
- Uses UTF-8 console encoding
- Requires PowerShell 7.5+ and .NET 9.0 SDK
