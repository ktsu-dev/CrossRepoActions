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
		Console.WriteLine($"Discovering repositories in {root}");
		return Directory.EnumerateDirectories(root, ".git", SearchOption.AllDirectories)
		.Select(p => p.As<AbsoluteDirectoryPath>().Parent);
	}

	internal static IEnumerable<string> Pull(AbsoluteDirectoryPath repo)
	{
		var ps = PowerShell.Create()
			.AddCommand("git")
			.AddArgument("-C")
			.AddArgument(repo.Replace("\\", "\\\\"))
			.AddArgument("pull")
			.AddArgument("--all")
			.AddArgument("-v");

		return ps.InvokeAndReturnOutput(PowershellStreams.All);
	}
}
