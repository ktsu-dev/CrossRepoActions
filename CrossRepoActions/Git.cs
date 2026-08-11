// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.CrossRepoActions;

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using ktsu.Extensions;
using ktsu.Semantics.Paths;
using ktsu.Semantics.Strings;

internal static class Git
{
	internal static IEnumerable<AbsoluteDirectoryPath> DiscoverRepositories(AbsoluteDirectoryPath root)
	{
		PersistentState persistentState = PersistentState.Get();
		if (persistentState.CachedRepos.Count > 0)
		{
			return persistentState.CachedRepos;
		}

		Console.WriteLine($"Discovering repositories in {root}");

		persistentState.CachedRepos = Directory.EnumerateDirectories(root, ".git", SearchOption.AllDirectories)
			.Select(p => p.As<AbsoluteDirectoryPath>().Parent)
			.ToCollection();

		persistentState.Save();

		return persistentState.CachedRepos;
	}

	// Transfer commands report their whole progress narrative on standard error, so these return
	// both streams. Callers scan the result for "error:" and for conflict and rejection markers.
	internal static IEnumerable<string> Pull(AbsoluteDirectoryPath repo) =>
		GitCli.Run(repo, "pull", "--all", "--autostash", "-v").AllLines;

	internal static IEnumerable<string> Fetch(AbsoluteDirectoryPath repo) =>
		GitCli.Run(repo, "fetch", "--all", "-v").AllLines;

	internal static IEnumerable<string> InstallLfs(AbsoluteDirectoryPath repo) =>
		GitCli.Run(repo, "lfs", "install", "--local").AllLines;

	internal static IEnumerable<string> Push(AbsoluteDirectoryPath repo) =>
		GitCli.Run(repo, "push", "-v").AllLines;

	/// <summary>
	/// Gets the short status of a single file. An empty result means the file is unmodified, which
	/// is how callers decide whether committing is safe.
	/// </summary>
	internal static IEnumerable<string> Status(AbsoluteDirectoryPath repo, AbsoluteFilePath filePath)
	{
		CommandResult result = GitCli.Run(repo, "status", "--short", "--", filePath.ToString());

		// Standard output alone on success: git reports line-ending conversions as warnings on
		// standard error, and counting one of those as a modification would block the commit.
		// On failure fall back to both streams so the result is non-empty and the caller still
		// treats the file as unsafe to commit rather than assuming it is clean.
		return result.Succeeded ? result.OutputLines : result.AllLines;
	}

	internal static IEnumerable<string> Unstage(AbsoluteDirectoryPath repo) =>
		GitCli.Run(repo, "restore", "--staged", repo.ToString()).AllLines;

	internal static IEnumerable<string> Add(AbsoluteDirectoryPath repo, AbsoluteFilePath filePath) =>
		GitCli.Run(repo, "add", filePath.ToString()).AllLines;

	internal static IEnumerable<string> Commit(AbsoluteDirectoryPath repo, string message) =>
		GitCli.Run(repo, "commit", "-m", message).AllLines;

	internal static string GetCurrentBranch(AbsoluteDirectoryPath repo)
	{
		CommandResult result = GitCli.Run(repo, "rev-parse", "--abbrev-ref", "HEAD");

		return result.Succeeded && result.OutputText.Length > 0 ? result.OutputText : "unknown";
	}

	internal static string GetStatusSummary(AbsoluteDirectoryPath repo)
	{
		CommandResult result = GitCli.Run(repo, "status", "--porcelain");
		if (!result.Succeeded)
		{
			return "unknown";
		}

		Collection<string> results = result.OutputLines;
		if (results.Count == 0)
		{
			return "clean";
		}

		// The status codes occupy two columns, but the lines have been trimmed, so a leading space
		// is gone and each state has to be recognised in both its staged and unstaged spelling.
		int modified = results.Count(s => s.StartsWith(" M") || s.StartsWith("M "));
		int added = results.Count(s => s.StartsWith("A ") || s.StartsWith("??"));
		int deleted = results.Count(s => s.StartsWith(" D") || s.StartsWith("D "));
		int renamed = results.Count(s => s.StartsWith("R "));

		List<string> parts = [];
		if (modified > 0)
		{
			parts.Add($"{modified}M");
		}

		if (added > 0)
		{
			parts.Add($"{added}A");
		}

		if (deleted > 0)
		{
			parts.Add($"{deleted}D");
		}

		if (renamed > 0)
		{
			parts.Add($"{renamed}R");
		}

		return parts.Count > 0 ? string.Join(" ", parts) : "modified";
	}

	/// <summary>
	/// Gets the upstream (tracking) branch for the current branch, e.g. "origin/main".
	/// Returns <see langword="null"/> if the current branch has no configured upstream.
	/// </summary>
	internal static string? GetUpstreamBranch(AbsoluteDirectoryPath repo)
	{
		// git exits non-zero when there is no upstream, so the exit code answers this directly
		// rather than having to sniff the message for "fatal".
		CommandResult result = GitCli.Run(repo, "rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{upstream}");

		return result.Succeeded && result.OutputText.Length > 0 ? result.OutputText : null;
	}

	/// <summary>
	/// Gets the repository's default branch name ("main" or "master"), preferring "main",
	/// based on which remote-tracking branch exists under "origin".
	/// Returns <see langword="null"/> if neither <c>origin/main</c> nor <c>origin/master</c> exists.
	/// </summary>
	internal static string? GetDefaultBranch(AbsoluteDirectoryPath repo)
	{
		foreach (string candidate in (string[])["main", "master"])
		{
			if (RefExists(repo, $"refs/remotes/origin/{candidate}"))
			{
				return candidate;
			}
		}

		return null;
	}

	/// <summary>
	/// Determines whether the given fully-qualified ref exists in the repository.
	/// </summary>
	internal static bool RefExists(AbsoluteDirectoryPath repo, string reference) =>
		GitCli.Run(repo, "rev-parse", "--verify", "--quiet", reference).Succeeded;

	/// <summary>
	/// Counts how many commits <paramref name="headRef"/> is ahead and behind
	/// <paramref name="baseRef"/>. Returns <see langword="null"/> if either ref cannot be resolved.
	/// </summary>
	internal static (int Ahead, int Behind)? GetAheadBehind(AbsoluteDirectoryPath repo, string baseRef, string headRef)
	{
		CommandResult result = GitCli.Run(repo, "rev-list", "--left-right", "--count", $"{baseRef}...{headRef}");
		if (!result.Succeeded)
		{
			return null;
		}

		string? line = result.OutputLines.FirstOrDefault();
		if (string.IsNullOrWhiteSpace(line))
		{
			return null;
		}

		// --left-right --count base...head prints "<behind>\t<ahead>":
		// left = commits reachable from base but not head, right = the reverse.
		string[] parts = line.Split(['\t', ' '], StringSplitOptions.RemoveEmptyEntries);
		return parts.Length == 2
			&& int.TryParse(parts[0], out int behind)
			&& int.TryParse(parts[1], out int ahead)
			? (ahead, behind)
			: null;
	}

	/// <summary>
	/// Gets how far the current branch is ahead/behind its upstream tracking branch.
	/// Returns <see langword="null"/> if there is no upstream configured.
	/// </summary>
	internal static (int Ahead, int Behind)? GetUpstreamAheadBehind(AbsoluteDirectoryPath repo)
	{
		string? upstream = GetUpstreamBranch(repo);
		return upstream is null ? null : GetAheadBehind(repo, upstream, "HEAD");
	}

	/// <summary>
	/// Determines whether the repository has any untracked (and non-gitignored) files.
	/// </summary>
	internal static bool HasUntrackedFiles(AbsoluteDirectoryPath repo) => GetUntrackedFiles(repo).Count > 0;

	private static Collection<string> GetUntrackedFiles(AbsoluteDirectoryPath repo) =>
		GitCli.Run(repo, "ls-files", "--others", "--exclude-standard").OutputLines;

	/// <summary>
	/// Gets the complete, untruncated working-tree diff against HEAD (staged + unstaged changes
	/// to tracked files). Untracked files are not included here — they are surfaced by name in
	/// <see cref="GetDiffStat"/> instead. Budgeting/truncation is handled separately by
	/// <see cref="Llm.DiffBudget"/> so it can drop whole files rather than clip a diff mid-file.
	/// </summary>
	internal static string GetFullDiff(AbsoluteDirectoryPath repo) =>
		// Returned verbatim rather than as trimmed lines. A diff carries meaning in its leading
		// column: context lines begin with a space, and trimming them away corrupts the diff.
		GitCli.Run(repo, "diff", "HEAD").Output;

	/// <summary>
	/// Gets the <c>git diff HEAD --stat</c> summary, with untracked file names appended so the
	/// summary reflects brand-new files too.
	/// </summary>
	internal static string GetDiffStat(AbsoluteDirectoryPath repo)
	{
		string stat = string.Join(Environment.NewLine, GitCli.Run(repo, "diff", "HEAD", "--stat").OutputLines);

		Collection<string> untracked = GetUntrackedFiles(repo);
		if (untracked.Count > 0)
		{
			stat += $"{Environment.NewLine}Untracked: {string.Join(", ", untracked)}";
		}

		return stat;
	}

	internal static IEnumerable<string> StageAll(AbsoluteDirectoryPath repo) =>
		GitCli.Run(repo, "add", "-A").AllLines;
}
