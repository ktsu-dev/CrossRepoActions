namespace ktsu.CrossRepoActions.Verbs;

using System.Collections.Concurrent;

using CommandLine;

using ktsu.Extensions;

[Verb("GitPull")]
internal class GitPull : BaseVerb<GitPull>
{
	internal override void Run(GitPull options)
	{
		ConcurrentBag<string> errorSummary = [];
		IEnumerable<StrongPaths.AbsoluteDirectoryPath> repos = Git.DiscoverRepositories(options.Path);
		_ = Parallel.ForEach(repos, new()
		{
			MaxDegreeOfParallelism = Program.MaxParallelism,
		},
		repo =>
		{
			// strip branch names from output because they could get confused with errors if they contain the word "error"
			string[] remoteBranches = [.. Git.BranchRemote(repo).Select(b => b.RemovePrefix("origin/"))];

			string[] output = [.. Git.Pull(repo).Select(s =>
			{
				string sanitized = s;
				foreach (string branch in remoteBranches)
				{
					sanitized = sanitized.Replace("\t", " ");
					while (sanitized.Contains("  "))
					{
						sanitized = sanitized.Replace("  ", " ");
					}

					sanitized = sanitized.Replace($"{branch} -> origin/{branch}", "");
				}

				return sanitized;
			})];

			if (output.Any(s => s.Contains("error")))
			{
				string error = $"❌ {System.IO.Path.GetFileName(repo)}";
				string errorOutput = string.Join("\n", output);
				errorSummary.Add($"{error} - {errorOutput}");
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
