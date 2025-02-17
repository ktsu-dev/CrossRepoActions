namespace ktsu.CrossRepoActions;

using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;

using ktsu.Extensions;
using ktsu.StrongPaths;

internal static class Git
{
	internal static IEnumerable<AbsoluteDirectoryPath> DiscoverRepositories(AbsoluteDirectoryPath root)
	{
		var persistentState = PersistentState.Get();
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
		using var ps = PowerShell.Create();
		var results = ps
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
		using var ps = PowerShell.Create();
		var results = ps
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
		using var ps = PowerShell.Create();
		var results = ps
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
		using var ps = PowerShell.Create();
		var results = ps
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
		using var ps = PowerShell.Create();
		var results = ps
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
		using var ps = PowerShell.Create();
		var results = ps
			.AddCommand("git")
			.AddArgument("-C")
			.AddArgument(repo.ToString())
			.AddArgument("commit")
			.AddParameter("-m", message)
			.InvokeAndReturnOutput(PowershellStreams.All);

		return results;
	}
}
