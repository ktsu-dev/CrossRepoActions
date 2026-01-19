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
			.AddArgument("-v")
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
}
