// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CrossRepoActions.Verbs;

using System.Collections.Generic;
using CommandLine;

using ktsu.Semantics.Paths;

[Verb("ListRepositories")]
internal sealed class ListRepositories : BaseVerb<ListRepositories>
{
	internal override void Run(ListRepositories options)
	{
		List<AbsoluteDirectoryPath> repos = [.. Git.DiscoverRepositories(options.Path)];

		Console.WriteLine($"Found {repos.Count} repositories:");
		Console.WriteLine();

		foreach (AbsoluteDirectoryPath repo in repos.OrderBy(r => System.IO.Path.GetFileName(r.ToString())))
		{
			string repoName = System.IO.Path.GetFileName(repo.ToString());
			string branch = Git.GetCurrentBranch(repo);
			string status = Git.GetStatusSummary(repo);

			string statusIndicator = status == "clean" ? "✓" : "●";
			Console.WriteLine($"{statusIndicator} {repoName,-40} [{branch,-20}] {status}");
		}
	}
}
