namespace ktsu.CrossRepoActions;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;

using DustInTheWind.ConsoleTools.Controls.Spinners;

using ktsu.Extensions;
using ktsu.RunCommand;
using ktsu.StrongPaths;

internal static class Dotnet
{
	internal static Collection<string> BuildSolution()
	{
		Collection<string> results = [];

		RunCommand.Execute("dotnet build --nologo", new LineOutputHandler(results.Add, results.Add));

		return GetErrors(results);
	}

	internal static Collection<string> BuildProject(AbsoluteFilePath projectFile)
	{
		Collection<string> results = [];

		RunCommand.Execute($"dotnet build --nologo {projectFile}", new LineOutputHandler(results.Add, results.Add));

		return GetErrors(results);
	}

	internal static Collection<string> RunTests()
	{
		Collection<string> results = [];

		RunCommand.Execute($"dotnet vstest **/bin/**/*Test.dll --logger:console;verbosity=normal --nologo", new LineOutputHandler(results.Add, results.Add));

		return GetErrors(results);
	}

	internal static Collection<string> GetTests()
	{
		Collection<string> results = [];

		RunCommand.Execute($"dotnet vstest --ListTests --nologo **/bin/**/*Test.dll", new LineOutputHandler(results.Add, results.Add));

		var filteredResults = results
			.Where(r => r is not null && !r.StartsWith("The following") && !r.StartsWith("No test source"))
			.ToCollection();

		return filteredResults;
	}

	internal static Collection<string> GetProjects(AbsoluteFilePath solutionFile)
	{
		Collection<string> results = [];

		RunCommand.Execute($"dotnet sln {solutionFile} list", new LineOutputHandler(results.Add, results.Add));

		var filteredResults = results
			.Where(r => r is not null && r.EndsWithOrdinal(".csproj"))
			.ToCollection();

		return filteredResults;
	}

	internal static Collection<Package> GetSolutionDependencies(AbsoluteFilePath solutionFile)
	{
		Collection<string> results = [];

		RunCommand.Execute($"dotnet list {solutionFile} package --include-transitive", new LineOutputHandler(results.Add, results.Add));

		var filteredResults = results
			.Where(r => r is not null && r.StartsWithOrdinal(">"))
			.ToCollection();

		var dependencies = filteredResults
			.Select(r =>
			{
				string[] parts = r.Split(' ');
				return new Package()
				{
					Name = parts[1],
					Version = parts.Last(),
				};
			})
			.ToCollection();

		return dependencies;
	}

	private const string packageJsonError = "Could not parse JSON output from 'dotnet list package --format-json'";
	internal static Collection<Package> GetOutdatedProjectDependencies(AbsoluteFilePath projectFile)
	{
		Collection<string> results = [];

		RunCommand.Execute($"dotnet list {projectFile} package --format=json", new LineOutputHandler(results.Add, results.Add));

		string jsonString = string.Join("", results);
		var rootObject = JsonNode.Parse(jsonString)?.AsObject()
			?? throw new InvalidDataException(packageJsonError);

		var projects = rootObject["projects"]?.AsArray()
			?? throw new InvalidDataException(packageJsonError);

		var frameworks = projects.Where(p =>
		{
			var pObj = p?.AsObject();
			return pObj?["frameworks"]?.AsArray() != null;
		})
		.SelectMany(p =>
		{
			return p?.AsObject()?["frameworks"]?.AsArray()
				?? throw new InvalidDataException(packageJsonError);
		});

		var packages = frameworks.SelectMany(f =>
		{
			return (f as JsonObject)?["topLevelPackages"]?.AsArray()
				?? throw new InvalidDataException(packageJsonError);
		})
		.Select(ExtractPackageFromJsonNode)
		.DistinctBy(p => p.Name)
		.ToCollection();

		return packages;
	}

	private static Package ExtractPackageFromJsonNode(JsonNode? p)
	{
		string name = p?["id"]?.AsValue().GetValue<string>()
			?? throw new InvalidDataException(packageJsonError);

		string version = p?["requestedVersion"]?.AsValue().GetValue<string>()
			?? throw new InvalidDataException(packageJsonError);

		return new Package()
		{
			Name = name,
			Version = version,
		};
	}

	internal static Collection<string> UpdatePackages(AbsoluteFilePath projectFile, IEnumerable<Package> packages)
	{
		// Find the solution file to determine if central package management is used
		var solutionPath = FindSolutionForProject(projectFile);
		if (solutionPath != null && UsesCentralPackageManagement(solutionPath))
		{
			// Use central package management update approach
			return UpdatePackagesWithCentralManagement(solutionPath, packages);
		}

		// Use traditional per-project update approach
		return UpdatePackagesTraditional(projectFile, packages);
	}

	/// <summary>
	/// Updates packages using traditional per-project approach.
	/// </summary>
	private static Collection<string> UpdatePackagesTraditional(AbsoluteFilePath projectFile, IEnumerable<Package> packages)
	{
		Collection<string> output = [];
		foreach (var package in packages)
		{
			Collection<string> results = [];
			string pre = package.Version.Contains('-') ? "--prerelease" : "";
			RunCommand.Execute($"dotnet add {projectFile} package {package.Name} {pre}", new LineOutputHandler(results.Add, results.Add));

			output.AddMany(results);
		}

		return output;
	}

	/// <summary>
	/// Updates packages using central package management.
	/// </summary>
	internal static Collection<string> UpdatePackagesWithCentralManagement(AbsoluteFilePath solutionPath, IEnumerable<Package> packages)
	{
		var directoryPackagesPath = GetDirectoryPackagesPath(solutionPath);
		if (directoryPackagesPath == null)
		{
			Collection<string> errorResult = [];
			errorResult.Add("Error: Central package management is enabled but Directory.Packages.props not found");
			return errorResult;
		}

		return UpdateCentralPackageVersions(directoryPackagesPath, packages);
	}

	/// <summary>
	/// Finds the solution file that contains the given project file.
	/// </summary>
	/// <param name="projectFile">Path to the project file</param>
	/// <returns>Path to the solution file or null if not found</returns>
	private static AbsoluteFilePath? FindSolutionForProject(AbsoluteFilePath projectFile)
	{
		var directory = projectFile.DirectoryPath;

		// Search upward from project directory for solution files
		var currentDir = directory;
		while (!string.IsNullOrEmpty(currentDir) && currentDir != currentDir.Parent)
		{
			var solutionFiles = Directory.EnumerateFiles(currentDir, "*.sln", SearchOption.TopDirectoryOnly);
			string? solutionFile = solutionFiles.FirstOrDefault();

			if (!string.IsNullOrEmpty(solutionFile))
			{
				return solutionFile.As<AbsoluteFilePath>();
			}

			currentDir = currentDir.Parent;
		}

		return null;
	}

	internal static string GetProjectAssemblyName(AbsoluteFilePath projectFile)
	{
		Collection<string> results = [];

		RunCommand.Execute($"dotnet msbuild {projectFile} -getProperty:AssemblyName", new LineOutputHandler(results.Add, results.Add));

		return results.First();
	}

	internal static string GetProjectVersion(AbsoluteFilePath projectFile)
	{
		Collection<string> results = [];

		RunCommand.Execute($"dotnet msbuild {projectFile} -getProperty:Version", new LineOutputHandler(results.Add, results.Add));

		return results.First();
	}

	internal static bool IsProjectPackable(AbsoluteFilePath projectFile)
	{
		Collection<string> results = [];

		RunCommand.Execute($"dotnet msbuild {projectFile} -getProperty:IsPackable", new LineOutputHandler(results.Add, results.Add));

		try
		{
			return bool.Parse(results.First());
		}
		catch (FormatException)
		{
			return false;
		}
	}

	internal static Package GetProjectPackage(AbsoluteFilePath projectFile)
	{
		return new Package()
		{
			Name = GetProjectAssemblyName(projectFile),
			Version = GetProjectVersion(projectFile)
		};
	}

	internal static Collection<string> GetErrors(IEnumerable<string> strings) =>
		strings.Where(r => r is not null && (r.Contains("error") || r.Contains("failed"))
						&& !(r.Contains("passed") || r.Contains("0 Error")))
			.ToCollection();

	private static object ConsoleLock { get; } = new();
	internal static Collection<Solution> DiscoverSolutionDependencies(IEnumerable<AbsoluteFilePath> solutionFiles)
	{
		var solutionFileCollection = solutionFiles.ToCollection();
		ConcurrentBag<Solution> solutions = [];

		ProgressBar progressBar = new();
		progressBar.Display();

		_ = Parallel.ForEach(solutionFileCollection, new()
		{
			//MaxDegreeOfParallelism = Program.MaxParallelism,
		},
		//solutionFileCollection.ForEach(
		solutionFile =>
		{
			var projects = GetProjects(solutionFile)
				.Select(p => solutionFile.DirectoryPath / p.As<RelativeFilePath>())
				.ToCollection();

			var packages = projects
				.Where(p => IsProjectPackable(p))
				.Select(p => GetProjectPackage(p))
				.ToCollection();

			var dependencies = GetSolutionDependencies(solutionFile);

			Solution solution = new()
			{
				Name = Path.GetFileNameWithoutExtension(solutionFile.FileName),
				Path = solutionFile,
				Projects = projects,
				Packages = packages,
				Dependencies = dependencies,
			};
			solutions.Add(solution);

			lock (ConsoleLock)
			{
				progressBar.Value = (int)Math.Round(solutions.Count / (float)solutionFileCollection.Count * 100);
				progressBar.Display();
			}
		});

		Console.WriteLine();
		Console.WriteLine();

		return solutions.ToCollection();
	}

	internal static Collection<Solution> SortSolutionsByDependencies(ICollection<Solution> solutions)
	{
		var unsatisfiedSolutions = solutions.ToCollection();
		Collection<Solution> sortedSolutions = [];

		while (unsatisfiedSolutions.Count != 0)
		{
			var unsatisfiedPackages = unsatisfiedSolutions
				.SelectMany(s => s.Packages)
				.ToCollection();

			var satisfied = unsatisfiedSolutions
				.Where(s => !s.Dependencies.IntersectBy(unsatisfiedPackages.Select(p => p.Name), p => p.Name).Any())
				.ToCollection();

			foreach (var solution in satisfied)
			{
				unsatisfiedSolutions.Remove(solution);
				sortedSolutions.Add(solution);
			}
		}

		return sortedSolutions;
	}

	internal static Collection<AbsoluteFilePath> DiscoverSolutionFiles(AbsoluteDirectoryPath root)
	{
		return Directory.EnumerateFiles(root, "*.sln", SearchOption.AllDirectories)
		.Select(p => p.As<AbsoluteFilePath>())
		.Where(p => !IsSolutionNested(p))
		.ToCollection();
	}

	internal static Collection<Solution> DiscoverSolutions(AbsoluteDirectoryPath root)
	{
		var persistentState = PersistentState.Get();
		if (persistentState.CachedSolutions.Count > 0)
		{
			return persistentState.CachedSolutions;
		}

		Console.WriteLine($"Discovering solutions in {root}");

		persistentState.CachedSolutions = SortSolutionsByDependencies(DiscoverSolutionDependencies(DiscoverSolutionFiles(root)));
		persistentState.Save();

		return persistentState.CachedSolutions;
	}

	internal static bool IsSolutionNested(AbsoluteFilePath solutionPath)
	{
		var solutionDir = solutionPath.DirectoryPath;
		var checkDir = solutionDir;
		do
		{
			checkDir = checkDir.Parent;
			if (Directory.EnumerateFiles(checkDir, "*.sln", SearchOption.TopDirectoryOnly).Any())
			{
				return true;
			}
		}
		while (Path.IsPathFullyQualified(checkDir));

		return false;
	}

	/// <summary>
	/// Checks if a solution/repository uses central package management.
	/// </summary>
	/// <param name="solutionPath">Path to the solution file</param>
	/// <returns>True if central package management is enabled</returns>
	internal static bool UsesCentralPackageManagement(AbsoluteFilePath solutionPath)
	{
		var solutionDir = solutionPath.DirectoryPath;

		// Check for Directory.Packages.props file
		var directoryPackagesPath = solutionDir / "Directory.Packages.props".As<RelativeFilePath>();
		if (File.Exists(directoryPackagesPath))
		{
			return true;
		}

		// Check for ManagePackageVersionsCentrally property in Directory.Build.props
		var directoryBuildPropsPath = solutionDir / "Directory.Build.props".As<RelativeFilePath>();
		if (File.Exists(directoryBuildPropsPath))
		{
			try
			{
				var doc = XDocument.Load(directoryBuildPropsPath);
				return doc.Descendants("ManagePackageVersionsCentrally")
					.Any(e => string.Equals(e.Value?.Trim(), "true", StringComparison.OrdinalIgnoreCase));
			}
			catch (Exception ex) when (ex is XmlException or IOException)
			{
				// If we can't parse the file, assume no central package management
				return false;
			}
		}

		return false;
	}

	/// <summary>
	/// Gets the path to the Directory.Packages.props file for a solution.
	/// </summary>
	/// <param name="solutionPath">Path to the solution file</param>
	/// <returns>Path to Directory.Packages.props or null if not found</returns>
	internal static AbsoluteFilePath? GetDirectoryPackagesPath(AbsoluteFilePath solutionPath)
	{
		var solutionDir = solutionPath.DirectoryPath;
		var directoryPackagesPath = solutionDir / "Directory.Packages.props".As<RelativeFilePath>();

		return File.Exists(directoryPackagesPath) ? directoryPackagesPath : null;
	}

	/// <summary>
	/// Gets package versions from Directory.Packages.props file.
	/// </summary>
	/// <param name="directoryPackagesPath">Path to Directory.Packages.props</param>
	/// <returns>Collection of packages with their centrally managed versions</returns>
	internal static Collection<Package> GetCentralPackageVersions(AbsoluteFilePath directoryPackagesPath)
	{
		if (!File.Exists(directoryPackagesPath))
		{
			return [];
		}

		try
		{
			var doc = XDocument.Load(directoryPackagesPath);
			var packages = doc.Descendants("PackageVersion")
				.Where(e => e.Attribute("Include") != null && e.Attribute("Version") != null)
				.Select(e => new Package
				{
					Name = e.Attribute("Include")!.Value,
					Version = e.Attribute("Version")!.Value
				})
				.ToCollection();

			return packages;
		}
		catch (Exception ex) when (ex is XmlException or IOException)
		{
			Console.WriteLine($"Warning: Could not parse Directory.Packages.props: {ex.Message}");
			return [];
		}
	}

	/// <summary>
	/// Updates package versions in Directory.Packages.props file.
	/// </summary>
	/// <param name="directoryPackagesPath">Path to Directory.Packages.props</param>
	/// <param name="packages">Packages to update</param>
	/// <returns>Collection of result messages</returns>
	internal static Collection<string> UpdateCentralPackageVersions(AbsoluteFilePath directoryPackagesPath, IEnumerable<Package> packages)
	{
		Collection<string> results = [];

		if (!File.Exists(directoryPackagesPath))
		{
			results.Add($"Error: Directory.Packages.props not found at {directoryPackagesPath}");
			return results;
		}

		try
		{
			var doc = XDocument.Load(directoryPackagesPath);
			bool modified = false;

			foreach (var package in packages)
			{
				var packageVersionElement = doc.Descendants("PackageVersion")
					.FirstOrDefault(e => string.Equals(e.Attribute("Include")?.Value, package.Name, StringComparison.OrdinalIgnoreCase));

				if (packageVersionElement != null)
				{
					string? currentVersion = packageVersionElement.Attribute("Version")?.Value;
					if (currentVersion != package.Version)
					{
						packageVersionElement.SetAttributeValue("Version", package.Version);
						results.Add($"Updated {package.Name} from {currentVersion} to {package.Version} in Directory.Packages.props");
						modified = true;
					}
					else
					{
						results.Add($"{package.Name} version {package.Version} is already up to date in Directory.Packages.props");
					}
				}
				else
				{
					// Add new package version if it doesn't exist
					var itemGroup = doc.Descendants("ItemGroup").FirstOrDefault();
					if (itemGroup == null)
					{
						// Create ItemGroup if it doesn't exist
						var project = doc.Element("Project");
						if (project != null)
						{
							itemGroup = new XElement("ItemGroup");
							project.Add(itemGroup);
						}
					}

					if (itemGroup != null)
					{
						var newPackageVersion = new XElement("PackageVersion");
						newPackageVersion.SetAttributeValue("Include", package.Name);
						newPackageVersion.SetAttributeValue("Version", package.Version);
						itemGroup.Add(newPackageVersion);
						results.Add($"Added {package.Name} version {package.Version} to Directory.Packages.props");
						modified = true;
					}
					else
					{
						results.Add($"Error: Could not add {package.Name} to Directory.Packages.props - no Project element found");
					}
				}
			}

			if (modified)
			{
				doc.Save(directoryPackagesPath);
				results.Add($"Saved changes to Directory.Packages.props");
			}
		}
		catch (Exception ex) when (ex is XmlException or IOException)
		{
			results.Add($"Error updating Directory.Packages.props: {ex.Message}");
		}

		return results;
	}

	/// <summary>
	/// Gets outdated package dependencies for projects using central package management.
	/// </summary>
	/// <param name="solutionPath">Path to the solution file</param>
	/// <returns>Collection of outdated packages</returns>
	internal static Collection<Package> GetOutdatedCentralPackageDependencies(AbsoluteFilePath solutionPath)
	{
		var directoryPackagesPath = GetDirectoryPackagesPath(solutionPath);
		if (directoryPackagesPath == null)
		{
			return [];
		}

		// Get current central package versions
		var centralPackages = GetCentralPackageVersions(directoryPackagesPath);

		// Get outdated packages from dotnet CLI
		string outdatedPackagesJson = GetOutdatedPackagesJson(solutionPath);
		if (string.IsNullOrEmpty(outdatedPackagesJson))
		{
			return [];
		}

		// Parse and filter outdated packages
		return ParseOutdatedPackages(outdatedPackagesJson, centralPackages);
	}

	/// <summary>
	/// Gets the JSON output from dotnet list package --outdated command.
	/// </summary>
	private static string GetOutdatedPackagesJson(AbsoluteFilePath solutionPath)
	{
		Collection<string> results = [];
		RunCommand.Execute($"dotnet list {solutionPath} package --outdated --format=json", new LineOutputHandler(results.Add, results.Add));
		return string.Join("", results);
	}

	/// <summary>
	/// Parses outdated packages JSON and filters for centrally managed packages.
	/// </summary>
	private static Collection<Package> ParseOutdatedPackages(string jsonString, Collection<Package> centralPackages)
	{
		try
		{
			var rootObject = JsonNode.Parse(jsonString)?.AsObject();
			if (rootObject == null)
			{
				return [];
			}

			var projects = rootObject["projects"]?.AsArray();
			if (projects == null)
			{
				return [];
			}

			var outdatedPackages = new Dictionary<string, Package>();

			foreach (var project in projects)
			{
				ProcessProjectForOutdatedPackages(project, centralPackages, outdatedPackages);
			}

			return outdatedPackages.Values.ToCollection();
		}
		catch (Exception ex) when (ex is JsonException or InvalidOperationException)
		{
			Console.WriteLine($"Warning: Could not parse outdated packages JSON: {ex.Message}");
			return [];
		}
	}

	/// <summary>
	/// Processes a single project node to find outdated packages.
	/// </summary>
	private static void ProcessProjectForOutdatedPackages(JsonNode? project, Collection<Package> centralPackages, Dictionary<string, Package> outdatedPackages)
	{
		var frameworks = project?.AsObject()?["frameworks"]?.AsArray();
		if (frameworks == null)
		{
			return;
		}

		foreach (var framework in frameworks)
		{
			ProcessFrameworkForOutdatedPackages(framework, centralPackages, outdatedPackages);
		}
	}

	/// <summary>
	/// Processes a single framework node to find outdated packages.
	/// </summary>
	private static void ProcessFrameworkForOutdatedPackages(JsonNode? framework, Collection<Package> centralPackages, Dictionary<string, Package> outdatedPackages)
	{
		var topLevelPackages = framework?.AsObject()?["topLevelPackages"]?.AsArray();
		if (topLevelPackages == null)
		{
			return;
		}

		foreach (var packageNode in topLevelPackages)
		{
			ProcessPackageNodeForOutdated(packageNode, centralPackages, outdatedPackages);
		}
	}

	/// <summary>
	/// Processes a single package node to check if it's outdated and centrally managed.
	/// </summary>
	private static void ProcessPackageNodeForOutdated(JsonNode? packageNode, Collection<Package> centralPackages, Dictionary<string, Package> outdatedPackages)
	{
		var packageObj = packageNode?.AsObject();
		if (packageObj == null)
		{
			return;
		}

		string? id = packageObj["id"]?.AsValue().GetValue<string>();
		string? latestVersion = packageObj["latestVersion"]?.AsValue().GetValue<string>();

		if (IsPackageOutdatedAndCentrallyManaged(id, latestVersion, centralPackages))
		{
			outdatedPackages[id!] = new Package { Name = id!, Version = latestVersion! };
		}
	}

	/// <summary>
	/// Checks if a package is outdated and centrally managed.
	/// </summary>
	private static bool IsPackageOutdatedAndCentrallyManaged(string? id, string? latestVersion, Collection<Package> centralPackages)
	{
		if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(latestVersion))
		{
			return false;
		}

		var centralPackage = centralPackages.FirstOrDefault(p => string.Equals(p.Name, id, StringComparison.OrdinalIgnoreCase));
		return centralPackage != null && centralPackage.Version != latestVersion;
	}
}
