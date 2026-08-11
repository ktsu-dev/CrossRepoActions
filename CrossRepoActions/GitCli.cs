// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.CrossRepoActions;

using ktsu.Semantics.Paths;

/// <summary>
/// Runs the git command line against a specific repository.
/// </summary>
internal static class GitCli
{
	/// <summary>
	/// Runs git against a repository using <c>-C</c>, which leaves the process working directory
	/// untouched and so stays safe when repositories are processed in parallel.
	/// </summary>
	/// <param name="repository">The repository to operate on.</param>
	/// <param name="arguments">The arguments to pass to git.</param>
	/// <returns>The exit code and captured output.</returns>
	internal static CommandResult Run(AbsoluteDirectoryPath repository, params string[] arguments)
	{
		Ensure.NotNull(arguments);

		return Cli.Run("git", ["-C", repository.ToString(), .. arguments]);
	}
}
