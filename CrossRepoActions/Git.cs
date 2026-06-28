// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CrossRepoActions;

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;

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

	internal static IEnumerable<string> Pull(AbsoluteDirectoryPath repo)
	{
		using PowerShell ps = PowerShell.Create();
		Collection<string> results = ps
			.AddCommand("git")
			.AddArgument("-C")
			.AddArgument(repo.ToString())
			.AddArgument("pull")
			.AddArgument("--all")
			.AddArgument("--autostash")
			.AddArgument("-v")
			.InvokeAndReturnOutput(PowershellStreams.All);

		return results;
	}

	internal static IEnumerable<string> Fetch(AbsoluteDirectoryPath repo)
	{
		using PowerShell ps = PowerShell.Create();
		Collection<string> results = ps
			.AddCommand("git")
			.AddArgument("-C")
			.AddArgument(repo.ToString())
			.AddArgument("fetch")
			.AddArgument("--all")
			.AddArgument("-v")
			.InvokeAndReturnOutput(PowershellStreams.All);

		return results;
	}

	internal static IEnumerable<string> InstallLfs(AbsoluteDirectoryPath repo)
	{
		using PowerShell ps = PowerShell.Create();
		Collection<string> results = ps
			.AddCommand("git")
			.AddArgument("-C")
			.AddArgument(repo.ToString())
			.AddArgument("lfs")
			.AddArgument("install")
			.AddArgument("--local")
			.InvokeAndReturnOutput(PowershellStreams.All);

		return results;
	}

	internal static IEnumerable<string> Push(AbsoluteDirectoryPath repo)
	{
		using PowerShell ps = PowerShell.Create();
		Collection<string> results = ps
			.AddCommand("git")
			.AddArgument("-C")
			.AddArgument(repo.ToString())
			.AddArgument("push")
			.AddArgument("-v")
			.InvokeAndReturnOutput(PowershellStreams.All);

		return results;
	}

	internal static IEnumerable<string> Status(AbsoluteDirectoryPath repo, AbsoluteFilePath filePath)
	{
		using PowerShell ps = PowerShell.Create();
		Collection<string> results = ps
			.AddCommand("git")
			.AddArgument("-C")
			.AddArgument(repo.ToString())
			.AddArgument("status")
			.AddArgument("--short")
			.AddArgument("--")
			.AddArgument(filePath.ToString())
			.InvokeAndReturnOutput(PowershellStreams.All);

		return results;
	}

	internal static IEnumerable<string> Unstage(AbsoluteDirectoryPath repo)
	{
		using PowerShell ps = PowerShell.Create();
		Collection<string> results = ps
			.AddCommand("git")
			.AddArgument("-C")
			.AddArgument(repo.ToString())
			.AddArgument("restore")
			.AddArgument("--staged")
			.AddArgument(repo.ToString())
			.InvokeAndReturnOutput(PowershellStreams.All);

		return results;
	}

	internal static IEnumerable<string> Add(AbsoluteDirectoryPath repo, AbsoluteFilePath filePath)
	{
		using PowerShell ps = PowerShell.Create();
		Collection<string> results = ps
			.AddCommand("git")
			.AddArgument("-C")
			.AddArgument(repo.ToString())
			.AddArgument("add")
			.AddArgument(filePath.ToString())
			.InvokeAndReturnOutput(PowershellStreams.All);

		return results;
	}

	internal static IEnumerable<string> Commit(AbsoluteDirectoryPath repo, string message)
	{
		using PowerShell ps = PowerShell.Create();
		Collection<string> results = ps
			.AddCommand("git")
			.AddArgument("-C")
			.AddArgument(repo.ToString())
			.AddArgument("commit")
			.AddParameter("-m", message)
			.InvokeAndReturnOutput(PowershellStreams.All);

		return results;
	}

	internal static string GetCurrentBranch(AbsoluteDirectoryPath repo)
	{
		using PowerShell ps = PowerShell.Create();
		Collection<string> results = ps
			.AddCommand("git")
			.AddArgument("-C")
			.AddArgument(repo.ToString())
			.AddArgument("rev-parse")
			.AddArgument("--abbrev-ref")
			.AddArgument("HEAD")
			.InvokeAndReturnOutput(PowershellStreams.All);

		return results.FirstOrDefault() ?? "unknown";
	}

	internal static string GetStatusSummary(AbsoluteDirectoryPath repo)
	{
		using PowerShell ps = PowerShell.Create();
		Collection<string> results = ps
			.AddCommand("git")
			.AddArgument("-C")
			.AddArgument(repo.ToString())
			.AddArgument("status")
			.AddArgument("--porcelain")
			.InvokeAndReturnOutput(PowershellStreams.All);

		if (results.Count == 0)
		{
			return "clean";
		}

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
		using PowerShell ps = PowerShell.Create();
		Collection<string> results = ps
			.AddCommand("git")
			.AddArgument("-C")
			.AddArgument(repo.ToString())
			.AddArgument("rev-parse")
			.AddArgument("--abbrev-ref")
			.AddArgument("--symbolic-full-name")
			.AddArgument("@{upstream}")
			.InvokeAndReturnOutput(PowershellStreams.All);

		string? result = results.FirstOrDefault()?.Trim();
		return string.IsNullOrWhiteSpace(result)
			|| result.Contains("fatal", StringComparison.OrdinalIgnoreCase)
			|| result.Contains("error", StringComparison.OrdinalIgnoreCase)
			? null
			: result;
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
	internal static bool RefExists(AbsoluteDirectoryPath repo, string reference)
	{
		using PowerShell ps = PowerShell.Create();
		Collection<string> results = ps
			.AddCommand("git")
			.AddArgument("-C")
			.AddArgument(repo.ToString())
			.AddArgument("rev-parse")
			.AddArgument("--verify")
			.AddArgument("--quiet")
			.AddArgument(reference)
			.InvokeAndReturnOutput(PowershellStreams.All);

		return results.Any(s => !string.IsNullOrWhiteSpace(s)
			&& !s.Contains("fatal", StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>
	/// Counts how many commits <paramref name="headRef"/> is ahead and behind
	/// <paramref name="baseRef"/>. Returns <see langword="null"/> if either ref cannot be resolved.
	/// </summary>
	internal static (int Ahead, int Behind)? GetAheadBehind(AbsoluteDirectoryPath repo, string baseRef, string headRef)
	{
		using PowerShell ps = PowerShell.Create();
		Collection<string> results = ps
			.AddCommand("git")
			.AddArgument("-C")
			.AddArgument(repo.ToString())
			.AddArgument("rev-list")
			.AddArgument("--left-right")
			.AddArgument("--count")
			.AddArgument($"{baseRef}...{headRef}")
			.InvokeAndReturnOutput(PowershellStreams.All);

		string? line = results.FirstOrDefault()?.Trim();
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

	private static IReadOnlyList<string> GetUntrackedFiles(AbsoluteDirectoryPath repo)
	{
		using PowerShell ps = PowerShell.Create();
		return [.. ps
			.AddCommand("git")
			.AddArgument("-C")
			.AddArgument(repo.ToString())
			.AddArgument("ls-files")
			.AddArgument("--others")
			.AddArgument("--exclude-standard")
			.InvokeAndReturnOutput(PowershellStreams.All)
			.Where(s => !string.IsNullOrWhiteSpace(s))];
	}

	/// <summary>
	/// Gets the working-tree diff against HEAD (staged + unstaged changes to tracked files),
	/// plus a list of untracked file paths (which <c>git diff HEAD</c> omits). The result is
	/// truncated to <paramref name="maxChars"/>; <c>Truncated</c> reports whether it was cut.
	/// </summary>
	internal static (string Diff, bool Truncated) GetDiff(AbsoluteDirectoryPath repo, int maxChars)
	{
		using PowerShell ps = PowerShell.Create();
		Collection<string> results = ps
			.AddCommand("git")
			.AddArgument("-C")
			.AddArgument(repo.ToString())
			.AddArgument("diff")
			.AddArgument("HEAD")
			.InvokeAndReturnOutput(PowershellStreams.All);

		string diff = string.Join(Environment.NewLine, results);

		IReadOnlyList<string> untracked = GetUntrackedFiles(repo);
		if (untracked.Count > 0)
		{
			diff += $"{Environment.NewLine}{Environment.NewLine}# Untracked files:{Environment.NewLine}"
				+ string.Join(Environment.NewLine, untracked);
		}

		bool truncated = diff.Length > maxChars;
		if (truncated)
		{
			diff = diff[..maxChars];
		}

		return (diff, truncated);
	}

	/// <summary>
	/// Gets the <c>git diff HEAD --stat</c> summary, with untracked file names appended so the
	/// summary reflects brand-new files too.
	/// </summary>
	internal static string GetDiffStat(AbsoluteDirectoryPath repo)
	{
		using PowerShell ps = PowerShell.Create();
		Collection<string> results = ps
			.AddCommand("git")
			.AddArgument("-C")
			.AddArgument(repo.ToString())
			.AddArgument("diff")
			.AddArgument("HEAD")
			.AddArgument("--stat")
			.InvokeAndReturnOutput(PowershellStreams.All);

		string stat = string.Join(Environment.NewLine, results);

		IReadOnlyList<string> untracked = GetUntrackedFiles(repo);
		if (untracked.Count > 0)
		{
			stat += $"{Environment.NewLine}Untracked: {string.Join(", ", untracked)}";
		}

		return stat;
	}

	internal static IEnumerable<string> StageAll(AbsoluteDirectoryPath repo)
	{
		using PowerShell ps = PowerShell.Create();
		return ps
			.AddCommand("git")
			.AddArgument("-C")
			.AddArgument(repo.ToString())
			.AddArgument("add")
			.AddArgument("-A")
			.InvokeAndReturnOutput(PowershellStreams.All);
	}

	/// <summary>
	/// Opens the configured git diff tool for the working tree. If no diff tool is configured,
	/// the returned output contains git's error text and the caller falls back to printing the diff.
	/// </summary>
	internal static IEnumerable<string> OpenDiffTool(AbsoluteDirectoryPath repo)
	{
		using PowerShell ps = PowerShell.Create();
		return ps
			.AddCommand("git")
			.AddArgument("-C")
			.AddArgument(repo.ToString())
			.AddArgument("difftool")
			.AddArgument("-d")
			.AddArgument("--no-prompt")
			.InvokeAndReturnOutput(PowershellStreams.All);
	}
}
