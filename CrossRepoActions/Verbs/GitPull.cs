// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CrossRepoActions.Verbs;

using System.Collections.Concurrent;
using System.Collections.Generic;
using CommandLine;

using ktsu.Extensions;
using ktsu.Semantics.Paths;

[Verb("GitPull")]
internal sealed class GitPull : BaseVerb<GitPull>
{
	internal override void Run(GitPull options)
	{
		ConcurrentBag<string> errorSummary = [];
		IEnumerable<AbsoluteDirectoryPath> repos = Git.DiscoverRepositories(options.Path);
		_ = Parallel.ForEach(repos, new()
		{
			MaxDegreeOfParallelism = Program.MaxParallelism,
		},
		repo =>
		{
			IEnumerable<string> output = Git.Pull(repo);
			//output.WriteItemsToConsole();

			if (output.Any(s => s.StartsWith("error:", StringComparison.OrdinalIgnoreCase)
				|| s.StartsWith("fatal:", StringComparison.OrdinalIgnoreCase)
				|| s.Contains("CONFLICT")))
			{
				string error = $"❌ {System.IO.Path.GetFileName(repo)}";
				errorSummary.Add(error);
				Console.WriteLine(error);
			}
			else
			{
				Console.WriteLine($"✅ {System.IO.Path.GetFileName(repo)}");
			}
		});

		if (!errorSummary.IsEmpty)
		{
			Console.WriteLine();
			Console.WriteLine("Failed:");
			Console.WriteLine("-------");
			errorSummary.WriteItemsToConsole();
		}
	}
}
