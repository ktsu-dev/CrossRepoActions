// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CrossRepoActions.Verbs;

using System.Collections.Concurrent;

using CommandLine;

using ktsu.Extensions;

[Verb("GitPull")]
internal class GitPull : BaseVerb<GitPull>
{
	internal override void Run(GitPull options)
	{
		var errorSummary = new ConcurrentBag<string>();
		var repos = Git.DiscoverRepositories(options.Path);
		_ = Parallel.ForEach(repos, new()
		{
			MaxDegreeOfParallelism = Program.MaxParallelism,
		},
		repo =>
		{
			var output = Git.Pull(repo);
			//output.WriteItemsToConsole();

			if (output.Any(s => s.Contains("error")))
			{
				var error = $"❌ {System.IO.Path.GetFileName(repo)}";
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
