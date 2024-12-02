namespace ktsu.CrossRepoActions;

using System.Collections.ObjectModel;
using ktsu.StrongPaths;

public class Solution
{
	public string Name { get; init; } = string.Empty;
	public AbsoluteFilePath Path { get; init; } = new();
	public Collection<AbsoluteFilePath> Projects { get; init; } = [];
	public Collection<Package> Packages { get; init; } = [];
	public Collection<Package> Dependencies { get; init; } = [];
}
