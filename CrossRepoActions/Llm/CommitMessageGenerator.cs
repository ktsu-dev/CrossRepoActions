// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CrossRepoActions.Llm;

using System.Text;

internal sealed class CommitMessageGenerator(ILlmService llmService)
{
	internal async Task<string> GenerateAsync(string diffStat, string diff, bool truncated, CancellationToken cancellationToken = default)
	{
		string raw = await llmService
			.CompleteAsync(BuildSystemPrompt(), BuildUserPrompt(diffStat, diff, truncated), cancellationToken)
			.ConfigureAwait(false);
		return CleanResponse(raw);
	}

	internal static string BuildSystemPrompt() =>
		"You are an expert software engineer writing a git commit message. " +
		"Analyse the supplied diff and produce a single commit message in Conventional Commits style. " +
		"The first line must be a concise summary of the form '<type>: <description>' " +
		"where type is one of feat, fix, docs, style, refactor, perf, test, build, ci, chore, " +
		"and must be no longer than 72 characters. " +
		"If the change warrants explanation, add a blank line followed by a short body. " +
		"Do NOT include a version tag such as [major], [minor], or [patch]. " +
		"Do NOT wrap the message in markdown code fences or quotation marks. " +
		"Respond with the commit message only.";

	internal static string BuildUserPrompt(string diffStat, string diff, bool truncated)
	{
		StringBuilder sb = new();
		sb.AppendLine("Summary of changes (git diff --stat):");
		sb.AppendLine(diffStat);
		sb.AppendLine();
		if (truncated)
		{
			sb.AppendLine("NOTE: the diff below was truncated because it was large; base your message on what is visible.");
		}

		sb.AppendLine("Diff:");
		sb.AppendLine(diff);
		return sb.ToString();
	}

	internal static string CleanResponse(string raw)
	{
		string text = (raw ?? "").Trim();

		// Strip a wrapping triple-backtick code fence (with optional language hint on the first line).
		if (text.StartsWith("```", StringComparison.Ordinal))
		{
			int firstNewline = text.IndexOf('\n');
			if (firstNewline >= 0)
			{
				text = text[(firstNewline + 1)..];
			}

			if (text.EndsWith("```", StringComparison.Ordinal))
			{
				text = text[..^3];
			}

			text = text.Trim();
		}

		// Strip a single pair of wrapping quotes.
		if (text.Length >= 2
			&& ((text[0] == '"' && text[^1] == '"') || (text[0] == '\'' && text[^1] == '\'')))
		{
			text = text[1..^1].Trim();
		}

		return text;
	}
}