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
- **Repository Status**: List repositories with branch, working-tree status, and ahead/behind tracking
- **AI Commit Messages**: Generate suggested Conventional Commits messages for repositories with uncommitted changes using a remote LLM (OpenAI via Microsoft Semantic Kernel), then review and commit/push them

## Requirements

- .NET 10.0 SDK or later
- PowerShell Core (cross-platform)
- Git
- An OpenAI API key (only required for the AI commit message feature)

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

# List repositories with branch and ahead/behind status
dotnet run --project CrossRepoActions/CrossRepoActions.csproj -- ListRepositories -p c:/dev/ktsu-dev

# Configure the OpenAI LLM settings (model, API key, etc.)
dotnet run --project CrossRepoActions/CrossRepoActions.csproj -- ConfigureLlm

# Suggest AI commit messages for repos with uncommitted changes
dotnet run --project CrossRepoActions/CrossRepoActions.csproj -- SuggestCommits -p c:/dev/ktsu-dev
```

## Available Commands

| Command | Description |
|---------|-------------|
| `Menu` | Launch interactive menu (default) |
| `DiscoverRepositories` | Scan directory tree for git repositories and cache results |
| `DiscoverSolutions` | Find all .NET solutions, analyze dependencies, and sort by build order |
| `ListRepositories` | List repositories with branch, working-tree status, and upstream/default ahead-behind |
| `BuildAndTest` | Build all discovered solutions and run tests with visual feedback |
| `GitPull` | Pull latest changes from all discovered repositories |
| `GitFetch` | Fetch from all remotes across all discovered repositories |
| `InstallGitLfs` | Install Git LFS hooks locally in each discovered repository |
| `UpdatePackages` | Update outdated NuGet packages across multiple projects |
| `ConfigureLlm` | Configure OpenAI LLM settings (model, organization, max diff size, API key) |
| `SuggestCommits` | Generate AI commit messages for repos with uncommitted changes, then review/commit/push |

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

### LLM Settings

The AI commit message feature talks to OpenAI through [Microsoft Semantic Kernel](https://github.com/microsoft/semantic-kernel). Settings (model, organization ID, maximum diff size, and API key) are stored in the same platform-specific app data location as the discovery cache. Configure them interactively with the `ConfigureLlm` command — the API key is entered masked and displayed masked thereafter. The default model is `gpt-5.4-mini`.

> The API key is stored in plaintext in app data (consistent with how the app caches other state). Repository diffs are sent to OpenAI only when you run `SuggestCommits`.

## AI Commit Messages

The `SuggestCommits` command generates suggested commit messages for every repository with uncommitted local changes:

```bash
# One-time setup
dotnet run --project CrossRepoActions/CrossRepoActions.csproj -- ConfigureLlm

# Generate and review suggestions
dotnet run --project CrossRepoActions/CrossRepoActions.csproj -- SuggestCommits -p c:/dev/ktsu-dev
```

The workflow is:

1. **Discover & filter** — find repositories whose working tree is not clean. Repositories that contain **untracked files are skipped** (and listed), because `git diff HEAD` doesn't include new files, so the model couldn't see their content even though `git add -A` would commit them. Commit, stash, or ignore new files first.
2. **Fetch** — fetch all remotes (in parallel, with a progress counter) so ahead/behind is accurate.
3. **Pull if behind** — for any repo behind its upstream, offer to pull (`--autostash`) before generating.
4. **Generate** — in parallel, send each repo's `git diff` to the model and produce a Conventional Commits message. The diff is fit to a character budget (`MaxDiffChars`) by including only **whole-file** diffs — a file whose diff would be clipped (and any after it) is dropped entirely rather than sent partially. No version tag is added — the CI `[major]`/`[minor]`/`[patch]` bump is inferred by PSBuild when absent.
5. **Review** — for each repo, see the target branch, ahead/behind, diff stat, and (if the diff was budgeted) how many characters were sent vs. the full diff and which files were not fully included, then choose:
   - `[C]ommit` — stage all and commit on the current branch
   - `[P]ush` — stage, commit, then push
   - `[E]dit` — replace the message
   - `[R]egenerate` — ask the model again
   - `[D]iff` — print the diff to the console
   - `[S]kip` — leave the repo untouched

   If the suggestion was generated from a budgeted (truncated) diff, choosing commit/push first offers to resend the **full** diff and regenerate so the message accounts for every change.

6. **Background execution** — `[C]ommit`/`[P]ush` run in the **background** so you can move straight to the next repo. Each background action re-fetches the repo and, if it is behind, auto-pulls with `--autostash` (a pull conflict or rejected push is recorded as a failure). When you finish reviewing, `SuggestCommits` waits for the background actions, prints a per-repo success/failure summary, and offers to walk through any failures interactively.

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
