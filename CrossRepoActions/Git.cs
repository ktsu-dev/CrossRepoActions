namespace ktsu.CrossRepoActions;

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using ktsu.Extensions;
using ktsu.RunCommand;
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
		Collection<string> results = [];

		RunCommand.Execute($"git -C {repo} pull --all -v", new LineOutputHandler(s => results.Add(s.Trim()), s => results.Add(s.Trim())));

		return results;
	}

	internal static IEnumerable<string> Push(AbsoluteDirectoryPath repo)
	{
		Collection<string> results = [];

		RunCommand.Execute($"git -C {repo} push -v", new LineOutputHandler(s => results.Add(s.Trim()), s => results.Add(s.Trim())));

		return results;
	}

	internal static IEnumerable<string> Status(AbsoluteDirectoryPath repo, AbsoluteFilePath filePath)
	{
		Collection<string> results = [];

		RunCommand.Execute($"git -C {repo} status --short -- {filePath}", new LineOutputHandler(s => results.Add(s.Trim()), s => results.Add(s.Trim())));

		return results;
	}

	internal static IEnumerable<string> Unstage(AbsoluteDirectoryPath repo)
	{
		Collection<string> results = [];

		RunCommand.Execute($"git -C {repo} restore --staged {repo}", new LineOutputHandler(s => results.Add(s.Trim()), s => results.Add(s.Trim())));

		return results;
	}

	internal static IEnumerable<string> Add(AbsoluteDirectoryPath repo, AbsoluteFilePath filePath)
	{
		Collection<string> results = [];

		RunCommand.Execute($"git -C {repo} add {filePath}", new LineOutputHandler(s => results.Add(s.Trim()), s => results.Add(s.Trim())));

		return results;
	}

	internal static IEnumerable<string> Commit(AbsoluteDirectoryPath repo, string message)
	{
		Collection<string> results = [];

		RunCommand.Execute($"git -C {repo} commit -m {message}", new LineOutputHandler(s => results.Add(s.Trim()), s => results.Add(s.Trim())));

		return results;
	}

	internal static IEnumerable<string> BranchRemote(AbsoluteDirectoryPath repo)
	{
		Collection<string> results = [];

		RunCommand.Execute($"git -C {repo} branch --remote", new LineOutputHandler(s => results.Add(s.Trim()), s => results.Add(s.Trim())));

		return results;
	}
}
