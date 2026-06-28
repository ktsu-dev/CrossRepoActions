// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CrossRepoActions.Verbs;

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

using CommandLine;

using ktsu.CrossRepoActions.Llm;
using ktsu.Semantics.Paths;

[Verb("SuggestCommits")]
internal sealed class SuggestCommits : BaseVerb<SuggestCommits>
{
	private static readonly Lock ConsoleLock = new();

	private sealed record Suggestion(
		AbsoluteDirectoryPath Repo,
		string Name,
		string DiffStat,
		string Message,
		bool Truncated,
		int SentDiffLength,
		int FullDiffLength,
		IReadOnlyList<string> OmittedFiles,
		string? Error);

	private sealed class ReviewState
	{
		internal ReviewState(Suggestion suggestion) => ApplyFresh(suggestion);

		internal string Message { get; private set; } = "";
		internal bool Truncated { get; private set; }
		internal int SentDiffLength { get; private set; }
		internal int FullDiffLength { get; private set; }
		internal IReadOnlyList<string> OmittedFiles { get; private set; } = [];

		internal void SetMessage(string message) => Message = message;

		internal void ApplyFresh(Suggestion suggestion)
		{
			Message = suggestion.Message;
			Truncated = suggestion.Truncated;
			SentDiffLength = suggestion.SentDiffLength;
			FullDiffLength = suggestion.FullDiffLength;
			OmittedFiles = suggestion.OmittedFiles;
		}
	}

	internal override void Run(SuggestCommits options)
	{
		LlmSettings settings = PersistentState.Get().Llm;
		if (string.IsNullOrWhiteSpace(settings.ApiKey))
		{
			Console.WriteLine("No OpenAI API key configured. Run the ConfigureLlm verb first.");
			return;
		}

		CommitMessageGenerator generator = new(new SemanticKernelLlmService(settings));

		List<AbsoluteDirectoryPath> dirty = [.. Git.DiscoverRepositories(options.Path)
			.Where(r => Git.GetStatusSummary(r) != "clean")];

		if (dirty.Count == 0)
		{
			Console.WriteLine("No repositories with outstanding local changes.");
			return;
		}

		FetchAll(dirty);
		List<AbsoluteDirectoryPath> toProcess = PromptPullWhereBehind(dirty);
		List<Suggestion> suggestions = GenerateAll(toProcess, generator, settings);
		Review(suggestions, generator, settings);
	}

	private static void FetchAll(List<AbsoluteDirectoryPath> repos)
	{
		int done = 0;
		int total = repos.Count;
		_ = Parallel.ForEach(repos, new() { MaxDegreeOfParallelism = Program.MaxParallelism }, repo =>
		{
			_ = Git.Fetch(repo).ToList();
			int n = Interlocked.Increment(ref done);
			lock (ConsoleLock)
			{
				Console.Write($"\rFetching {n}/{total}…   ");
			}
		});

		Console.WriteLine($"\rFetching done ({total}).        ");
	}

	private static List<AbsoluteDirectoryPath> PromptPullWhereBehind(List<AbsoluteDirectoryPath> repos)
	{
		List<AbsoluteDirectoryPath> result = [];
		int total = repos.Count;
		for (int i = 0; i < total; i++)
		{
			AbsoluteDirectoryPath repo = repos[i];
			string name = System.IO.Path.GetFileName(repo.ToString());
			(int Ahead, int Behind)? ab = Git.GetUpstreamAheadBehind(repo);
			if (ab is { Behind: > 0 })
			{
				string? upstream = Git.GetUpstreamBranch(repo);
				Console.Write($"[{i + 1}/{total}] {name} is behind {upstream} by {ab.Value.Behind}. Pull before generating? [Y/n] ");
				string ans = (Console.ReadLine() ?? "").Trim().ToUpperInvariant();
				if (ans is not ("N" or "NO"))
				{
					List<string> pullOutput = [.. Git.Pull(repo)];
					if (HasConflict(pullOutput))
					{
						Console.WriteLine($"  pull conflicted in {name} — skipping this repo.");
						continue;
					}
				}
			}

			result.Add(repo);
		}

		return result;
	}

	private static List<Suggestion> GenerateAll(List<AbsoluteDirectoryPath> repos, CommitMessageGenerator generator, LlmSettings settings)
	{
		int done = 0;
		int total = repos.Count;
		ConcurrentBag<Suggestion> bag = [];
		_ = Parallel.ForEach(repos, new() { MaxDegreeOfParallelism = Program.MaxParallelism }, repo =>
		{
			bag.Add(GenerateOne(repo, generator, settings));
			int n = Interlocked.Increment(ref done);
			lock (ConsoleLock)
			{
				Console.Write($"\rGenerating {n}/{total}…   ");
			}
		});

		Console.WriteLine($"\rGenerating done ({total}).        ");
		return [.. bag];
	}

	[SuppressMessage("Design", "CA1031:Do not catch general exception types",
		Justification = "A per-repo generation failure (network, auth, parsing) must not abort the whole batch; the error is surfaced to the user in the review phase.")]
	private static Suggestion GenerateOne(AbsoluteDirectoryPath repo, CommitMessageGenerator generator, LlmSettings settings, bool fullDiff = false)
	{
		string name = System.IO.Path.GetFileName(repo.ToString());
		try
		{
			string stat = Git.GetDiffStat(repo);
			string full = Git.GetFullDiff(repo);
			int cap = fullDiff ? int.MaxValue : settings.MaxDiffChars;
			DiffBudget.Result budget = DiffBudget.Apply(full, cap);
			string message = generator.GenerateAsync(stat, budget.PromptDiff, budget.Truncated).GetAwaiter().GetResult();
			return new(repo, name, stat, message, budget.Truncated, budget.PromptDiff.Length, budget.FullLength, budget.OmittedFiles, null);
		}
		catch (Exception ex)
		{
			return new(repo, name, "", "", false, 0, 0, [], ex.Message);
		}
	}

	private static void Review(List<Suggestion> suggestions, CommitMessageGenerator generator, LlmSettings settings)
	{
		List<Suggestion> ordered = [.. suggestions.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)];
		int total = ordered.Count;
		for (int i = 0; i < total; i++)
		{
			ReviewOne(ordered[i], i + 1, total, generator, settings);
		}
	}

	private static void ReviewOne(Suggestion suggestion, int index, int total, CommitMessageGenerator generator, LlmSettings settings)
	{
		Console.WriteLine();
		Console.WriteLine($"[{index}/{total}] {suggestion.Name}");

		if (suggestion.Error is not null)
		{
			Console.WriteLine($"  generation failed: {suggestion.Error}");
			return;
		}

		string branch = Git.GetCurrentBranch(suggestion.Repo);
		string? upstream = Git.GetUpstreamBranch(suggestion.Repo);
		(int Ahead, int Behind)? ab = Git.GetUpstreamAheadBehind(suggestion.Repo);
		string branchLine = upstream is null
			? $"  branch: {branch} (no upstream)"
			: $"  branch: {branch} → {upstream} {Formatting.FormatAheadBehind(ab)}";
		Console.WriteLine(branchLine);

		Console.WriteLine("  changes:");
		foreach (string line in suggestion.DiffStat.Split('\n'))
		{
			Console.WriteLine($"    {line.TrimEnd()}");
		}

		ReviewState state = new(suggestion);
		bool resolved = false;
		while (!resolved)
		{
			if (state.Truncated)
			{
				Console.WriteLine($"  ⚠ diff truncated: sent {state.SentDiffLength} of {state.FullDiffLength} chars; "
					+ $"{state.OmittedFiles.Count} file(s) not fully included: {string.Join(", ", state.OmittedFiles)}");
			}

			Console.WriteLine();
			Console.WriteLine("  suggested message:");
			Console.WriteLine($"    {state.Message.Replace("\n", "\n    ", StringComparison.Ordinal)}");
			Console.Write("  [C]ommit / [P]ush / [E]dit / [R]egenerate / [D]iff / [S]kip ? ");
			string choice = (Console.ReadLine() ?? "").Trim().ToUpperInvariant();
			switch (choice)
			{
				case "C":
				case "P":
				{
					bool push = choice == "P";
					if (state.Truncated && !ConfirmTruncatedCommit(suggestion.Repo, state, generator, settings))
					{
						break;
					}

					resolved = CommitRepo(suggestion.Repo, state.Message, push);
					break;
				}

				case "E":
					state.SetMessage(EditMessage(state.Message));
					break;
				case "R":
					Regenerate(suggestion.Repo, generator, settings, state, fullDiff: false);
					break;
				case "D":
					ShowDiff(suggestion.Repo);
					break;
				case "S":
					Console.WriteLine("  skipped.");
					resolved = true;
					break;
				default:
					Console.WriteLine("  unrecognised choice.");
					break;
			}
		}
	}

	/// <summary>
	/// When a suggestion was generated from a truncated diff, gives the user the chance to resend
	/// the complete diff before committing. Returns <see langword="true"/> to proceed with the
	/// commit as-is, or <see langword="false"/> to return to the review prompt (cancelled, or the
	/// message was regenerated from the full diff and should be re-read first).
	/// </summary>
	private static bool ConfirmTruncatedCommit(AbsoluteDirectoryPath repo, ReviewState state, CommitMessageGenerator generator, LlmSettings settings)
	{
		Console.WriteLine($"  diff was truncated — sent {state.SentDiffLength} of {state.FullDiffLength} chars.");
		if (state.OmittedFiles.Count > 0)
		{
			Console.WriteLine($"  not fully included: {string.Join(", ", state.OmittedFiles)}");
		}

		Console.Write($"  Submit the FULL diff ({state.FullDiffLength} chars) and regenerate first? [F]ull / commit [A]s-is / [C]ancel ? ");
		string ans = (Console.ReadLine() ?? "").Trim().ToUpperInvariant();
		switch (ans)
		{
			case "A":
				return true;
			case "F":
				Regenerate(repo, generator, settings, state, fullDiff: true);
				return false;
			default:
				Console.WriteLine("  cancelled.");
				return false;
		}
	}

	private static void Regenerate(AbsoluteDirectoryPath repo, CommitMessageGenerator generator, LlmSettings settings, ReviewState state, bool fullDiff)
	{
		Console.WriteLine(fullDiff ? "  regenerating with the full diff…" : "  regenerating…");
		Suggestion fresh = GenerateOne(repo, generator, settings, fullDiff);
		if (fresh.Error is not null)
		{
			Console.WriteLine($"  regeneration failed: {fresh.Error}");
			return;
		}

		state.ApplyFresh(fresh);
	}

	private static bool CommitRepo(AbsoluteDirectoryPath repo, string message, bool push)
	{
		if (!EnsureRemoteCurrent(repo))
		{
			return false;
		}

		foreach (string line in Git.StageAll(repo))
		{
			Console.WriteLine($"    {line}");
		}

		foreach (string line in Git.Commit(repo, message))
		{
			Console.WriteLine($"    {line}");
		}

		if (push)
		{
			List<string> pushOutput = [.. Git.Push(repo)];
			foreach (string line in pushOutput)
			{
				Console.WriteLine($"    {line}");
			}

			if (HasPushRejection(pushOutput))
			{
				Console.WriteLine("  ✗ commit saved locally, but the push was rejected — pull/resolve and push manually.");
				return true;
			}
		}

		Console.WriteLine(push ? "  committed and pushed." : "  committed.");
		return true;
	}

	private static bool EnsureRemoteCurrent(AbsoluteDirectoryPath repo)
	{
		Console.WriteLine("  fetching latest from remote…");
		_ = Git.Fetch(repo).ToList();

		(int Ahead, int Behind)? ab = Git.GetUpstreamAheadBehind(repo);
		if (ab is { Behind: > 0 })
		{
			Console.Write($"  remote has {ab.Value.Behind} new commit(s). Pull (--autostash) before continuing? [Y/n] ");
			string ans = (Console.ReadLine() ?? "").Trim().ToUpperInvariant();
			if (ans is "N" or "NO")
			{
				Console.WriteLine("  cancelled.");
				return false;
			}

			List<string> pullOutput = [.. Git.Pull(repo)];
			foreach (string line in pullOutput)
			{
				Console.WriteLine($"    {line}");
			}

			if (HasConflict(pullOutput))
			{
				Console.WriteLine("  pull resulted in conflicts — resolve manually. Action aborted.");
				return false;
			}
		}

		return true;
	}

	private static string EditMessage(string current)
	{
		Console.WriteLine("  enter the new commit message; finish with an empty line:");
		List<string> lines = [];
		string? line;
		while (!string.IsNullOrEmpty(line = Console.ReadLine()))
		{
			lines.Add(line);
		}

		string edited = string.Join("\n", lines).Trim();
		return string.IsNullOrWhiteSpace(edited) ? current : edited;
	}

	private static void ShowDiff(AbsoluteDirectoryPath repo)
	{
		string diff = Git.GetFullDiff(repo);
		if (string.IsNullOrWhiteSpace(diff))
		{
			Console.WriteLine("  (no tracked changes to show — see the untracked files in the changes summary above)");
			return;
		}

		Console.WriteLine();
		Console.WriteLine(diff);
		Console.WriteLine();
	}

	private static bool HasConflict(IEnumerable<string> gitOutput) =>
		gitOutput.Any(l =>
			l.Contains("CONFLICT", StringComparison.OrdinalIgnoreCase)
			|| l.Contains("Automatic merge failed", StringComparison.OrdinalIgnoreCase));

	private static bool HasPushRejection(IEnumerable<string> gitOutput) =>
		gitOutput.Any(l =>
			l.Contains("[rejected]", StringComparison.OrdinalIgnoreCase)
			|| l.Contains("failed to push", StringComparison.OrdinalIgnoreCase)
			|| l.Contains("non-fast-forward", StringComparison.OrdinalIgnoreCase)
			|| l.Contains("fatal:", StringComparison.OrdinalIgnoreCase));
}
