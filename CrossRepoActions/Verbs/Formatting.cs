// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.CrossRepoActions.Verbs;

internal static class Formatting
{
	internal static string FormatAheadBehind((int Ahead, int Behind)? aheadBehind)
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
