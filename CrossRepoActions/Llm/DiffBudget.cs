// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CrossRepoActions.Llm;

using System.Collections.Generic;

/// <summary>
/// Fits a git diff within a character budget by including only whole-file diffs.
/// If a file's diff would be clipped by the budget, that file (and every file after it)
/// is omitted entirely rather than sent partially, so the prompt is always a clean prefix
/// of complete per-file diffs.
/// </summary>
internal static class DiffBudget
{
	internal sealed record Result(
		string PromptDiff,
		bool Truncated,
		int FullLength,
		IReadOnlyList<string> IncludedFiles,
		IReadOnlyList<string> OmittedFiles);

	private const string FileHeaderPrefix = "diff --git ";

	/// <summary>
	/// Applies the <paramref name="maxChars"/> budget to <paramref name="fullDiff"/>.
	/// Files are included in order while their complete diff fits; the first file that would
	/// overflow — and all files after it — are omitted.
	/// </summary>
	internal static Result Apply(string fullDiff, int maxChars)
	{
		string text = fullDiff ?? "";
		int fullLength = text.Length;

		List<(int Start, string? File)> segments = SplitSegments(text);
		if (segments.Count == 0)
		{
			return new("", false, fullLength, [], []);
		}

		List<string> included = [];
		List<string> omitted = [];
		int includedEnd = 0;
		bool stopped = false;

		for (int i = 0; i < segments.Count; i++)
		{
			int start = segments[i].Start;
			int end = i + 1 < segments.Count ? segments[i + 1].Start : fullLength;
			string? file = segments[i].File;

			if (!stopped && end <= maxChars)
			{
				includedEnd = end;
				if (file is not null)
				{
					included.Add(file);
				}
			}
			else
			{
				stopped = true;
				if (file is not null)
				{
					omitted.Add(file);
				}
			}
		}

		string promptDiff = text[..includedEnd];
		bool truncated = includedEnd < fullLength;
		return new(promptDiff, truncated, fullLength, included, omitted);
	}

	/// <summary>
	/// Splits the diff into per-file segments, each represented by its start index and file
	/// name. A leading preamble (content before the first <c>diff --git</c> header) is returned
	/// as a fileless segment with a <see langword="null"/> file.
	/// </summary>
	private static List<(int Start, string? File)> SplitSegments(string fullDiff)
	{
		List<(int Start, string? File)> segments = [];
		if (fullDiff.Length == 0)
		{
			return segments;
		}

		List<int> headerStarts = [];
		int searchFrom = 0;
		while (true)
		{
			int idx = fullDiff.IndexOf(FileHeaderPrefix, searchFrom, StringComparison.Ordinal);
			if (idx < 0)
			{
				break;
			}

			if (idx == 0 || fullDiff[idx - 1] == '\n')
			{
				headerStarts.Add(idx);
			}

			searchFrom = idx + FileHeaderPrefix.Length;
		}

		if (headerStarts.Count == 0)
		{
			// No recognizable file headers: treat the whole thing as one fileless segment.
			segments.Add((0, null));
			return segments;
		}

		// Leading preamble before the first file header (rare, e.g. warning text).
		if (headerStarts[0] > 0)
		{
			segments.Add((0, null));
		}

		foreach (int start in headerStarts)
		{
			segments.Add((start, ExtractFileName(fullDiff, start)));
		}

		return segments;
	}

	private static string ExtractFileName(string fullDiff, int headerStart)
	{
		int lineEnd = fullDiff.IndexOf('\n', headerStart);
		string line = lineEnd < 0 ? fullDiff[headerStart..] : fullDiff[headerStart..lineEnd];
		line = line.TrimEnd('\r');

		// Format: "diff --git a/<old> b/<new>" — take the new (b/) path.
		int bIdx = line.LastIndexOf(" b/", StringComparison.Ordinal);
		if (bIdx >= 0)
		{
			return line[(bIdx + 3)..].Trim();
		}

		// Fallback: strip the header prefix.
		return line.Length > FileHeaderPrefix.Length ? line[FileHeaderPrefix.Length..].Trim() : line;
	}
}
