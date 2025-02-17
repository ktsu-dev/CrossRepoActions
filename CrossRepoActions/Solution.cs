namespace ktsu.CrossRepoActions;

using System.Collections.ObjectModel;

using ktsu.StrongPaths;

/// <summary>
/// Represents a solution containing projects, packages, and dependencies.
/// </summary>
public class Solution
{
	/// <summary>
	/// Gets the name of the solution.
	/// </summary>
	public string Name { get; init; } = string.Empty;

	/// <summary>
	/// Gets the path to the solution file.
	/// </summary>
	public AbsoluteFilePath Path { get; init; } = new();

	/// <summary>
	/// Gets the collection of project file paths in the solution.
	/// </summary>
	public Collection<AbsoluteFilePath> Projects { get; init; } = [];

	/// <summary>
	/// Gets the collection of packages used in the solution.
	/// </summary>
	public Collection<Package> Packages { get; init; } = [];

	/// <summary>
	/// Gets the collection of dependencies for the solution.
	/// </summary>
	public Collection<Package> Dependencies { get; init; } = [];
}
