# CrossRepoActions

A powerful .NET console application for automating operations across multiple Git repositories. CrossRepoActions helps you manage, build, test, and maintain a collection of related repositories from a single interface.

## Features

- **Repository Discovery**: Automatically find all Git repositories within a directory tree
- **Solution Management**: Discover and manage .NET solutions across multiple repositories
- **Bulk Operations**: Build and test all solutions in parallel
- **Package Management**: Automatically update NuGet packages across repositories with Git commits
- **Git Operations**: Pull all repositories at once
- **Interactive Menu**: User-friendly console menu for selecting operations
- **Persistent Caching**: Remember discovered repositories and solutions for faster subsequent runs

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [PowerShell 7.5+](https://github.com/PowerShell/PowerShell)
- Git command-line tools

## Installation

### Clone and Build

```bash
git clone https://github.com/ktsu-dev/CrossRepoActions.git
cd CrossRepoActions
dotnet build
```

### Run

```bash
dotnet run
```

## Usage

### Interactive Menu (Default)

Run without arguments to launch the interactive menu:

```bash
dotnet run
```

This displays a menu with all available commands. Use arrow keys to navigate and Enter to select.

### Command Line Interface

You can also run specific commands directly:

```bash
# Discover repositories in default path (c:/dev/ktsu-dev)
dotnet run -- DiscoverRepositories

# Discover repositories in custom path
dotnet run -- DiscoverRepositories -p "c:/your/path"

# Build and test all solutions
dotnet run -- BuildAndTest

# Pull all repositories
dotnet run -- GitPull

# Start package update monitor
dotnet run -- UpdatePackages
```

## Available Commands

### DiscoverRepositories
Scans a directory tree for Git repositories and caches the results.

```bash
dotnet run -- DiscoverRepositories -p "c:/dev/ktsu-dev"
```

**Options:**
- `-p, --path`: Root path to search for repositories (default: `c:/dev/ktsu-dev`)

### DiscoverSolutions
Finds all .NET solution files within discovered repositories.

```bash
dotnet run -- DiscoverSolutions
```

### BuildAndTest
Builds all discovered solutions and runs their tests. Shows real-time progress with status indicators.

```bash
dotnet run -- BuildAndTest
```

**Output:**
- 🛠️ Building...
- ✅ Build succeeded
- ❌ Build failed
- 🧪 Running tests

### UpdatePackages
Continuously monitors and updates outdated NuGet packages across all solutions. Automatically commits updates to Git if the project file wasn't already modified.

```bash
dotnet run -- UpdatePackages
```

**Features:**
- Checks for outdated packages every 5 minutes
- Attempts to update each package individually
- Auto-commits successful updates
- Reports errors for failed updates

**Output:**
- ✅ Package up-to-date
- 🚀 Package updated
- ❌ Update failed

### GitPull
Pulls all discovered repositories with verbose output.

```bash
dotnet run -- GitPull
```

### Menu
Displays the interactive menu (default command).

```bash
dotnet run -- Menu
```

## Configuration

The application stores its persistent state (cached repositories and solutions) using the ktsu.AppDataStorage library. The cache is automatically saved to your user's application data directory.

To refresh the cache, run `DiscoverRepositories` again.

## Typical Workflow

1. **Initial Setup**: Run the application and select `DiscoverRepositories` to scan your development directory
2. **Build Verification**: Select `BuildAndTest` to ensure all solutions compile and tests pass
3. **Maintenance**: Run `UpdatePackages` to keep dependencies current across all repositories
4. **Sync**: Use `GitPull` periodically to sync all repositories with their remotes

## Development

### Project Structure

```
CrossRepoActions/
├── CrossRepoActions/
│   ├── Program.cs           # Entry point and verb loading
│   ├── PersistentState.cs   # Cached state management
│   ├── Git.cs               # Git operations via PowerShell
│   ├── Dotnet.cs            # .NET CLI operations
│   ├── Solution.cs          # Solution model
│   ├── Package.cs           # Package model
│   └── Verbs/               # Command implementations
│       ├── BaseVerb.cs
│       ├── Menu.cs
│       ├── DiscoverRepositories.cs
│       ├── DiscoverSolutions.cs
│       ├── BuildAndTest.cs
│       ├── UpdatePackages.cs
│       └── GitPull.cs
├── scripts/
│   └── PSBuild.psm1         # CI/CD PowerShell module
└── .github/workflows/       # GitHub Actions workflows
```

### Adding New Commands

1. Create a new class in `CrossRepoActions/Verbs/` inheriting from `BaseVerb<T>`
2. Add the `[Verb("CommandName")]` attribute
3. Implement the `Run(T options)` method
4. The command will automatically appear in the menu

Example:

```csharp
using CommandLine;

[Verb("MyCommand")]
internal class MyCommand : BaseVerb<MyCommand>
{
    internal override void Run(MyCommand options)
    {
        // Your implementation
    }
}
```

### Building

```bash
dotnet build --nologo
```

### Testing

```bash
dotnet test --nologo
```

## CI/CD

This project uses a custom PowerShell-based CI/CD pipeline (`scripts/PSBuild.psm1`) that handles:

- Automated versioning from Git tags
- Build and test execution
- Code coverage with OpenCover
- SonarQube integration
- NuGet package publishing
- GitHub release creation

See `.github/workflows/dotnet.yml` for the complete workflow.

## License

MIT License - see [LICENSE.md](LICENSE.md) for details

Copyright (c) 2023-2026 ktsu-dev

## Links

- **Repository**: https://github.com/ktsu-dev/CrossRepoActions
- **Issues**: https://github.com/ktsu-dev/CrossRepoActions/issues
- **Changelog**: [CHANGELOG.md](CHANGELOG.md)
