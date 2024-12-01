namespace ktsu.CrossRepoActions;

using NuGet.Versioning;

internal class Package
{
	internal string Name { get; set; } = string.Empty;
	internal NuGetVersion Version { get; set; } = new("0.0.0");
}
