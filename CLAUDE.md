# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

CrossRepoActions is a C# console application for performing batch operations across multiple git repositories and .NET solutions. It provides an interactive menu-driven interface for discovering repositories, building/testing solutions, updating packages, and managing git operations at scale.

**Key Purpose**: Automate repetitive tasks across an entire organization's repositories (default path: `c:/dev/ktsu-dev`).

## Build & Development Commands

### Basic Commands
```bash
# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run the application (launches interactive menu by default)
dotnet run --project CrossRepoActions/CrossRepoActions.csproj

# Run with specific verb
dotnet run --project CrossRepoActions/CrossRepoActions.csproj -- BuildAndTest -p c:/dev/ktsu-dev
dotnet run --project CrossRepoActions/CrossRepoActions.csproj -- DiscoverRepositories -p c:/dev/ktsu-dev
dotnet run --project CrossRepoActions/CrossRepoActions.csproj -- UpdatePackages -p c:/dev/ktsu-dev
```

### Testing
No test projects currently exist in this solution.

## Architecture

### Project Structure

The application uses a **verb-based command architecture** powered by CommandLineParser:

- **Program.cs**: Entry point that dynamically discovers and loads all verb types from assembly attributes
- **Verbs/**: Command implementations (each decorated with `[Verb]` attribute)
  - `BaseVerb`: Abstract base class for all commands
  - `BaseVerb<T>`: Generic base with validation and execution lifecycle
  - `Menu`: Default verb that displays interactive menu using DustInTheWind.ConsoleTools
  - `BuildAndTest`: Builds all discovered solutions and runs tests
  - `DiscoverRepositories`: Scans directory tree for git repositories
  - `DiscoverSolutions`: Finds and analyzes .NET solutions
  - `GitPull`: Pulls latest changes from all repositories
  - `UpdatePackages`: Updates NuGet packages across solutions
- **PersistentState.cs**: App data storage for caching discovered repos/solutions (uses ktsu.AppDataStorage)
- **Git.cs**: Git operations wrapper using PowerShell automation
- **Dotnet.cs**: .NET CLI operations wrapper using PowerShell automation
- **Solution.cs**: Data model representing a solution with projects, packages, and dependencies
- **PowershellExtensions.cs**: Extension methods for PowerShell invocation

### Key Patterns

1. **Dynamic Verb Discovery**: All verb types are automatically discovered via reflection from `[Verb]` attributes. Adding a new verb class automatically adds it to the menu.

2. **PowerShell Integration**: Both Git and Dotnet operations use `System.Management.Automation.PowerShell` for command execution, with custom extensions for output capture (PowershellExtensions.cs:18).

3. **Caching**: Repository and solution discovery results are cached in persistent app data to avoid repeated expensive file system scans. Cache is cleared when running `DiscoverRepositories` verb.

4. **Parallel Processing**: Solution discovery and dependency analysis use `Parallel.ForEach` with configurable `MaxParallelism` (Program.cs:19).

5. **Dependency Ordering**: Solutions are topologically sorted based on inter-project dependencies (Dotnet.cs:328) so builds happen in the correct order.

## PSBuild Module

This repository includes a comprehensive PowerShell build automation module in `scripts/PSBuild.psm1` used by CI/CD:

### Key Features
- **Git-based versioning**: Analyzes commit messages and code diffs to automatically determine semantic version increments
- **Version tags**: Commit messages can include `[major]`, `[minor]`, `[patch]`, or `[pre]` to control versioning
- **Public API detection**: Automatically triggers minor version bumps when public API surface changes
- **Metadata management**: Auto-generates VERSION.md, CHANGELOG.md, AUTHORS.md, LICENSE.md, COPYRIGHT.md, PROJECT_URL.url, AUTHORS.url

### CI/CD Pipeline (GitHub Actions)
The `.github/workflows/dotnet.yml` workflow orchestrates:
1. Build, test with code coverage
2. SonarQube analysis (if SONAR_TOKEN secret is set)
3. NuGet package creation and publishing
4. GitHub release creation with assets
5. Winget manifest updates
6. Security dependency scanning

**Important**: The PSBuild module handles all versioning, changelog generation, and release processes. Manual version changes should not be committed.

## Dependencies & SDK

- **Target Framework**: .NET 9.0 (global.json:3)
- **Custom SDKs**: Uses ktsu.Sdk.App/1.8.0 (project-specific SDK for build configuration)
- **Key Dependencies**:
  - CommandLineParser: CLI argument parsing
  - ConsoleTools (DustInTheWind): Interactive menus and spinners
  - Microsoft.PowerShell.SDK: PowerShell automation
  - ktsu.StrongPaths: Strongly-typed path handling
  - ktsu.AppDataStorage: Persistent state management
  - NuGet.Versioning: Version comparison and parsing

**Package Management**: Uses Central Package Management (Directory.Packages.props) with `ManagePackageVersionsCentrally=true`.

## Development Notes

### Adding a New Verb
1. Create a new class in `Verbs/` inheriting from `BaseVerb<T>`
2. Add `[Verb("VerbName")]` attribute
3. Implement `Run(T options)` method
4. Optionally override `ValidateArgs()` for parameter validation
5. The verb will automatically appear in the interactive menu

### Working with Git/Dotnet Operations
- All operations use PowerShell for cross-platform consistency
- Git operations use `-C` flag to operate on remote directories without changing process CWD
- Dotnet operations capture both stdout and stderr using custom extensions
- Error detection looks for "error" or "failed" in output (Dotnet.cs:274)

### Persistent State
- Stored using ktsu.AppDataStorage in platform-specific app data directory
- Contains `CachedRepos` and `CachedSolutions` collections
- Access via `PersistentState.Get()` singleton
- Cleared automatically when running discovery verbs

### Parallel Execution
- MaxParallelism is set to -1 (unlimited) in Program.cs:19
- Used for solution discovery and dependency analysis
- Console output is synchronized using locks (Dotnet.cs:279)
