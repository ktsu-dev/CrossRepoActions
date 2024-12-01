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
		_ = Parallel.ForEach(repos, repo =>
		{
			var output = Git.Pull(repo);
			//output.WriteItemsToConsole();

			if (output.Any(s => s.Contains("error")))
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
