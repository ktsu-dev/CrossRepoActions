// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CrossRepoActions;
/// <summary>
/// Represents a package with a name and version.
/// </summary>
public class Package
{
	/// <summary>
	/// Gets or sets the name of the package.
	/// </summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the version of the package.
	/// </summary>
	public string Version { get; set; } = "0.0.0";
}
