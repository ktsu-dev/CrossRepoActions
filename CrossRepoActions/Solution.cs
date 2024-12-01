namespace ktsu.CrossRepoActions;

using System.Collections.ObjectModel;
using ktsu.StrongPaths;

internal class Solution
{
	internal string Name { get; init; } = string.Empty;
	internal AbsoluteFilePath Path { get; init; } = new();
	internal Collection<AbsoluteFilePath> Projects { get; init; } = [];
	internal Collection<Package> Packages { get; init; } = [];
	internal Collection<Package> Dependencies { get; init; } = [];
}
