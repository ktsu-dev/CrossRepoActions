# CrossRepoActions

CrossRepoActions is a powerful .NET console application designed to perform batch operations across multiple repositories and solutions. It streamlines common development tasks by automating actions across your entire development workspace.

## Features

### 🚀 Core Commands

- **UpdatePackages** - Automatically update NuGet packages across multiple solutions with support for both traditional and central package management
- **BuildAndTest** - Build and test multiple solutions in parallel with detailed status reporting
- **GitPull** - Pull changes from multiple Git repositories simultaneously
- **Menu** - Interactive menu-driven interface for easy command selection
- **DiscoverRepositories** - Discover all Git repositories in a directory tree
- **DiscoverSolutions** - Discover all .NET solutions in a directory tree

### ✨ Key Benefits

- **Parallel Processing** - Leverages multi-threading for fast execution across multiple repositories
- **Central Package Management Support** - Full support for .NET's central package management features
- **Smart Error Handling** - Comprehensive error reporting and recovery
- **Interactive Interface** - Menu-driven interface for ease of use
- **Flexible Path Configuration** - Configurable root paths for repository discovery

## Installation

### Prerequisites

- .NET 9.0 or later
- Git (for repository operations)
- PowerShell (for certain operations)

### Building from Source

1. Clone the repository:
   ```bash
   git clone https://github.com/ktsu-dev/CrossRepoActions.git
   cd CrossRepoActions
   ```

2. Build the application:
   ```bash
   dotnet build
   ```

3. Run the application:
   ```bash
   dotnet run --project CrossRepoActions
   ```

## Usage

### Interactive Menu (Default)

Simply run the application without arguments to access the interactive menu:

```bash
CrossRepoActions
```

or explicitly:

```bash
CrossRepoActions Menu
```

### Command Line Interface

#### Update Packages Across Repositories

```bash
CrossRepoActions UpdatePackages --path "C:\dev\my-projects"
```

This command will:
- Discover all .NET solutions in the specified path
- Check for outdated NuGet packages
- Update packages automatically
- Handle both traditional and central package management scenarios
- Commit changes to Git when appropriate

#### Build and Test Multiple Solutions

```bash
CrossRepoActions BuildAndTest --path "C:\dev\my-projects"
```

This command will:
- Build all discovered solutions
- Run tests for each project
- Provide detailed status reporting
- Generate error summaries for failed builds

#### Pull Changes from Multiple Repositories

```bash
CrossRepoActions GitPull --path "C:\dev\my-projects"
```

This command will:
- Discover all Git repositories in the specified path
- Execute `git pull` on each repository in parallel
- Report success/failure status for each repository
- Provide detailed error information for failed pulls

#### Discover Repositories and Solutions

```bash
# Discover all Git repositories
CrossRepoActions DiscoverRepositories --path "C:\dev\my-projects"

# Discover all .NET solutions
CrossRepoActions DiscoverSolutions --path "C:\dev\my-projects"
```

### Configuration

#### Default Path Configuration

The application uses a default path of `c:/dev/ktsu-dev` for repository discovery. You can override this using the `--path` or `-p` option:

```bash
CrossRepoActions UpdatePackages -p "C:\your\custom\path"
```

#### Persistent Settings

CrossRepoActions maintains persistent settings using the `ktsu.AppDataStorage` library. Settings are automatically saved and restored between sessions.

## Command Options

### Global Options

- `-p, --path` - The root path to discover solutions/repositories from (default: `c:/dev/ktsu-dev`)

### Package Update Features

- **Central Package Management**: Automatically detects and handles solutions using central package management
- **Smart Committing**: Only commits changes when the working directory is clean
- **Parallel Processing**: Updates multiple solutions simultaneously for improved performance
- **Detailed Reporting**: Provides comprehensive status updates and error reporting

## Dependencies

CrossRepoActions leverages several key libraries:

- **CommandLineParser** (2.9.1) - Command-line argument parsing
- **ConsoleTools** (1.2.1) - Interactive console menus
- **ktsu.AppDataStorage** (1.15.6) - Persistent application settings
- **ktsu.Extensions** (1.5.6) - Utility extensions
- **ktsu.RunCommand** (1.3.1) - External command execution
- **Microsoft.PowerShell.SDK** (7.5.2) - PowerShell integration
- **NuGet.Versioning** (6.14.0) - NuGet package version handling

## Development

### Project Structure

```
CrossRepoActions/
├── Verbs/              # Command implementations
│   ├── BaseVerb.cs     # Base command class
│   ├── UpdatePackages.cs
│   ├── BuildAndTest.cs
│   ├── GitPull.cs
│   ├── Menu.cs
│   └── ...
├── Dotnet.cs           # .NET CLI operations
├── Git.cs              # Git operations
├── Package.cs          # Package management
├── Solution.cs         # Solution discovery
└── Program.cs          # Application entry point
```

### Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## License

This project is licensed under the MIT License. See [LICENSE.md](LICENSE.md) for details.

## Version

Current version: 1.2.2-pre.3

---

**CrossRepoActions** - Streamlining multi-repository development workflows since 2023.
