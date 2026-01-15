# CrossRepoActions

A powerful C# console application for performing batch operations across multiple git repositories and .NET solutions. CrossRepoActions helps you manage an entire organization's codebase efficiently with an interactive menu-driven interface.

## Features

- **Repository Discovery**: Automatically discover all git repositories in a directory tree
- **Solution Management**: Find and analyze .NET solutions with dependency resolution
- **Batch Building & Testing**: Build and test multiple solutions in dependency order
- **Package Management**: Update NuGet packages across multiple projects simultaneously
- **Git Operations**: Pull changes from multiple repositories in one command
- **Smart Caching**: Cache discovery results for faster subsequent operations
- **Interactive Menu**: User-friendly console menu for easy operation selection
- **Parallel Processing**: Efficient parallel execution of operations where possible

## Requirements

- .NET 9.0 SDK or later
- PowerShell Core (cross-platform)
- Git

## Installation

1. Clone the repository:
```bash
git clone https://github.com/ktsu-dev/CrossRepoActions.git
cd CrossRepoActions
```

2. Restore dependencies:
```bash
dotnet restore
```

3. Build the application:
```bash
dotnet build
```

## Usage

### Interactive Menu (Default)

Run the application without arguments to launch the interactive menu:

```bash
dotnet run --project CrossRepoActions/CrossRepoActions.csproj
```

Use arrow keys to navigate and Enter to select an operation.

### Command Line Interface

Run specific operations directly from the command line:

```bash
# Discover all repositories in a directory
dotnet run --project CrossRepoActions/CrossRepoActions.csproj -- DiscoverRepositories -p c:/dev/ktsu-dev

# Discover all solutions
dotnet run --project CrossRepoActions/CrossRepoActions.csproj -- DiscoverSolutions -p c:/dev/ktsu-dev

# Build and test all solutions
dotnet run --project CrossRepoActions/CrossRepoActions.csproj -- BuildAndTest -p c:/dev/ktsu-dev

# Pull latest changes from all repositories
dotnet run --project CrossRepoActions/CrossRepoActions.csproj -- GitPull -p c:/dev/ktsu-dev

# Update packages across solutions
dotnet run --project CrossRepoActions/CrossRepoActions.csproj -- UpdatePackages -p c:/dev/ktsu-dev
```

## Available Commands

| Command | Description |
|---------|-------------|
| `Menu` | Launch interactive menu (default) |
| `DiscoverRepositories` | Scan directory tree for git repositories and cache results |
| `DiscoverSolutions` | Find all .NET solutions, analyze dependencies, and sort by build order |
| `BuildAndTest` | Build all discovered solutions and run tests with visual feedback |
| `GitPull` | Pull latest changes from all discovered repositories |
| `UpdatePackages` | Update outdated NuGet packages across multiple projects |

## Configuration

### Default Path

The default repository discovery path is `c:/dev/ktsu-dev`. Override this using the `-p` or `--path` option:

```bash
dotnet run --project CrossRepoActions/CrossRepoActions.csproj -- BuildAndTest -p /path/to/your/repos
```

### Persistent State

CrossRepoActions caches discovered repositories and solutions for faster subsequent operations. Cache is stored in platform-specific app data directory and automatically refreshed when running discovery commands.

### Parallel Processing

Operations are executed in parallel where possible for maximum performance. The degree of parallelism is configurable in `Program.cs`.

## How It Works

1. **Discovery**: Recursively scans directories for `.git` folders and `.sln` files
2. **Analysis**: Examines each solution to identify projects, packages, and dependencies
3. **Dependency Sorting**: Topologically sorts solutions so dependencies are built before dependents
4. **Execution**: Runs operations in optimal order with parallel processing where appropriate
5. **Feedback**: Provides real-time progress with spinners, progress bars, and status indicators

## Examples

### Build All Solutions in Order

```bash
dotnet run --project CrossRepoActions/CrossRepoActions.csproj -- BuildAndTest -p c:/dev/myorg
```

Output includes:
- Build status indicators (🛠️ in progress, ✅ success, ❌ error)
- Per-project build results
- Test discovery and execution results
- Summary of all errors

### Update Packages Organization-Wide

```bash
dotnet run --project CrossRepoActions/CrossRepoActions.csproj -- UpdatePackages -p c:/dev/myorg
```

This will:
1. Discover all solutions
2. Identify outdated packages
3. Update packages while respecting prerelease settings
4. Handle each project's dependencies correctly

## Development

See [CLAUDE.md](CLAUDE.md) for detailed architecture documentation and development guidance.

### Adding a New Command

1. Create a new class in `CrossRepoActions/Verbs/`
2. Inherit from `BaseVerb<YourVerb>`
3. Add the `[Verb("YourCommandName")]` attribute
4. Implement the `Run(YourVerb options)` method
5. The command automatically appears in the interactive menu

Example:
```csharp
[Verb("MyCommand")]
internal class MyCommand : BaseVerb<MyCommand>
{
    internal override void Run(MyCommand options)
    {
        // Your implementation
    }
}
```

## License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## Links

- [GitHub Repository](https://github.com/ktsu-dev/CrossRepoActions)
- [ktsu-dev Organization](https://github.com/ktsu-dev)
