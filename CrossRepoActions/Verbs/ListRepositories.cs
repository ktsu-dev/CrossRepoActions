// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CrossRepoActions.Verbs;

using System.Collections.Concurrent;
using System.Collections.Generic;
using CommandLine;

using ktsu.Semantics.Paths;

[Verb("ListRepositories")]
internal sealed class ListRepositories : BaseVerb<ListRepositories>
{
	[Option("no-fetch", Required = false, HelpText = "Skip fetching from remotes before reporting ahead/behind status.")]
	public bool NoFetch { get; set; }

	private sealed record RepoStatus(
		string Name,
		string Branch,
		string WorkingStatus,
		(int Ahead, int Behind)? Upstream,
		string? DefaultRef,
		(int Ahead, int Behind)? VsDefault);

	internal override void Run(ListRepositories options)
	{
		List<AbsoluteDirectoryPath> repos = [.. Git.DiscoverRepositories(options.Path)];

		if (!options.NoFetch)
		{
			Console.WriteLine($"Fetching {repos.Count} repositories...");
		}

		ConcurrentBag<RepoStatus> statuses = [];
		_ = Parallel.ForEach(repos, new()
		{
			MaxDegreeOfParallelism = Program.MaxParallelism,
		},
		repo =>
		{
			if (!options.NoFetch)
			{
				_ = Git.Fetch(repo).ToList();
			}

			string name = System.IO.Path.GetFileName(repo.ToString());
			string branch = Git.GetCurrentBranch(repo);
			string workingStatus = Git.GetStatusSummary(repo);
			(int Ahead, int Behind)? upstream = Git.GetUpstreamAheadBehind(repo);

			string? defaultBranch = Git.GetDefaultBranch(repo);
			string? defaultRef = defaultBranch is not null
				&& !branch.Equals(defaultBranch, StringComparison.OrdinalIgnoreCase)
				? $"origin/{defaultBranch}"
				: null;
			(int Ahead, int Behind)? vsDefault = defaultRef is not null
				? Git.GetAheadBehind(repo, defaultRef, "HEAD")
				: null;

			statuses.Add(new(name, branch, workingStatus, upstream, defaultRef, vsDefault));
		});

		Console.WriteLine();
		Console.WriteLine($"Found {repos.Count} repositories:");
		Console.WriteLine("(upstream: ↑ahead ↓behind vs tracking branch, ≡ in sync, — no upstream)");
		Console.WriteLine();

		foreach (RepoStatus status in statuses.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
		{
			string statusIndicator = status.WorkingStatus == "clean" ? "✓" : "●";
			string upstreamStr = FormatAheadBehind(status.Upstream);

			string vsDefaultStr = status.VsDefault is not null
				? $"  {status.DefaultRef}: {FormatAheadBehind(status.VsDefault)}"
				: "";

			Console.WriteLine(
				$"{statusIndicator} {status.Name,-40} [{status.Branch,-24}] {status.WorkingStatus,-12} {upstreamStr,-10}{vsDefaultStr}");
		}
	}

	private static string FormatAheadBehind((int Ahead, int Behind)? aheadBehind)
	{
		if (aheadBehind is null)
		{
			return "—";
		}

		(int ahead, int behind) = aheadBehind.Value;
		if (ahead == 0 && behind == 0)
		{
			return "≡";
		}

		List<string> parts = [];
		if (ahead > 0)
		{
			parts.Add($"↑{ahead}");
		}

		if (behind > 0)
		{
			parts.Add($"↓{behind}");
		}

		return string.Join(" ", parts);
	}
}
