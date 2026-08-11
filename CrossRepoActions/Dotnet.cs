// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.CrossRepoActions;

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Nodes;

using DustInTheWind.ConsoleTools.Controls.Spinners;

using ktsu.Extensions;
using ktsu.Semantics.Paths;
using ktsu.Semantics.Strings;

using NuGet.Versioning;

internal static class Dotnet
{
	// These two run against whatever the process working directory is, which BuildAndTest sets per
	// solution before calling in. A started process inherits it, exactly as the PowerShell host did.
	internal static Collection<string> BuildSolution() =>
		GetErrors(Cli.Run("dotnet", "build", "--nologo").AllLines);

	internal static Collection<string> BuildProject(AbsoluteFilePath projectFile) =>
		GetErrors(Cli.Run("dotnet", "build", "--nologo", projectFile.ToString()).AllLines);

	internal static Collection<string> RunTests() =>
		Cli.Run("dotnet", "vstest", "**/bin/**/*Test.dll", "/logger:console;verbosity=normal", "--nologo").AllLines;

	internal static Collection<string> GetTests()
	{
		Collection<string> results = Cli.Run("dotnet", "vstest", "--ListTests", "--nologo", "**/bin/**/*Test.dll").AllLines;

		Collection<string> stringResults = results
			.Where(r => !r.StartsWith("The following") && !r.StartsWith("No test source"))
			.ToCollection();

		return stringResults;
	}

	internal static Collection<string> GetProjects(AbsoluteFilePath solutionFile)
	{
		Collection<string> results = Cli.Run("dotnet", "sln", solutionFile.ToString(), "list").OutputLines;

		Collection<string> stringResults = results
			.Where(r => r.EndsWithOrdinal(".csproj"))
			.ToCollection();

		return stringResults;
	}

	internal static Collection<Package> GetSolutionDependencies(AbsoluteFilePath solutionFile)
	{
		Collection<string> results = Cli.Run("dotnet", "list", solutionFile.ToString(), "package", "--include-transitive").OutputLines;

		Collection<string> stringResults = results
			.Where(r => r.StartsWithOrdinal(">"))
			.ToCollection();

		Collection<Package> dependencies = stringResults
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

	internal static Collection<Package> GetOutdatedProjectDependencies(AbsoluteFilePath projectFile)
	{
		// Standard output alone: this is parsed as JSON, and any diagnostic mixed into it would
		// make the document unparseable.
		Collection<string> jsonResult = Cli.Run("dotnet", "list", projectFile.ToString(), "package", "--outdated", "--format=json").OutputLines;

		string jsonString = string.Join("", jsonResult);

		// If the output is empty or doesn't contain valid JSON, return empty collection
		if (string.IsNullOrWhiteSpace(jsonString) || !jsonString.TrimStart().StartsWith('{'))
		{
			return [];
		}

		JsonObject? rootObject;
		try
		{
			rootObject = JsonNode.Parse(jsonString)?.AsObject();
		}
		catch (System.Text.Json.JsonException)
		{
			// If JSON parsing fails, return empty collection (no outdated packages or error in output)
			return [];
		}

		if (rootObject == null)
		{
			return [];
		}

		JsonArray? projects = rootObject["projects"]?.AsArray();
		if (projects == null)
		{
			return [];
		}

		IEnumerable<JsonNode?> frameworks = projects.Where(p =>
		{
			JsonObject? pObj = p?.AsObject();
			return pObj?["frameworks"]?.AsArray() != null;
		})
		.SelectMany(p => p?.AsObject()?["frameworks"]?.AsArray() ?? []);

		Collection<Package> packages = frameworks.SelectMany(f => (f as JsonObject)?["topLevelPackages"]?.AsArray() ?? [])
		.Select(p =>
		{
			string? name = p?["id"]?.AsValue().GetValue<string>();
			string? version = p?["requestedVersion"]?.AsValue().GetValue<string>();

			if (name == null || version == null)
			{
				return null;
			}

			return new Package()
			{
				Name = name,
				Version = version,
			};
		})
		.Where(p => p != null)
		.DistinctBy(p => p!.Name)
		.ToCollection()!;

		return packages;
	}

	internal static Collection<string> UpdatePackages(AbsoluteFilePath projectFile, IEnumerable<Package> packages)
	{
		Collection<string> output = [];
		foreach (Package package in packages)
		{
			bool isPreRelease = NuGetVersion.Parse(package.Version).IsPrerelease;
			string[] arguments = isPreRelease
				? ["add", projectFile.ToString(), "package", package.Name, "--prerelease"]
				: ["add", projectFile.ToString(), "package", package.Name];

			output.AddFrom(Cli.Run("dotnet", arguments).AllLines);
		}

		return output;
	}

	internal static string GetProjectAssemblyName(AbsoluteFilePath projectFile) =>
		GetProjectProperty(projectFile, "AssemblyName");

	internal static string GetProjectVersion(AbsoluteFilePath projectFile) =>
		GetProjectProperty(projectFile, "Version");

	internal static bool IsProjectPackable(AbsoluteFilePath projectFile) =>
		bool.TryParse(GetProjectProperty(projectFile, "IsPackable"), out bool isPackable) && isPackable;

	/// <summary>
	/// Reads a single evaluated MSBuild property from a project. Returns an empty string when the
	/// evaluation fails, which callers treat as the property being absent.
	/// </summary>
	private static string GetProjectProperty(AbsoluteFilePath projectFile, string propertyName)
	{
		CommandResult result = Cli.Run("dotnet", "msbuild", projectFile.ToString(), $"-getProperty:{propertyName}");

		return result.Succeeded ? result.OutputText : string.Empty;
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
		strings.Where(r => (r.Contains("error") || r.Contains("failed"))
						&& !(r.Contains("passed") || r.Contains("0 Error")))
			.ToCollection();

	private static object ConsoleLock { get; } = new();
	internal static Collection<Solution> DiscoverSolutionDependencies(IEnumerable<AbsoluteFilePath> solutionFiles)
	{
		Collection<AbsoluteFilePath> solutionFileCollection = solutionFiles.ToCollection();
		ConcurrentBag<Solution> solutions = [];

		ProgressBar progressBar = new();
		progressBar.Display();

		_ = Parallel.ForEach(solutionFileCollection, new()
		{
			//MaxDegreeOfParallelism = Program.MaxParallelism,
		},
		solutionFile =>
		{
			AbsoluteDirectoryPath solutionDirectoryPath = solutionFile.DirectoryPath.AsAbsolute();
			Collection<AbsoluteFilePath> projects = GetProjects(solutionFile)
				.Select(p => solutionDirectoryPath / p.As<RelativeFilePath>())
				.ToCollection();

			Collection<Package> packages = projects
				.Where(p => IsProjectPackable(p))
				.Select(p => GetProjectPackage(p))
				.ToCollection();

			Collection<Package> dependencies = GetSolutionDependencies(solutionFile);

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
		Collection<Solution> unsatisfiedSolutions = solutions.ToCollection();
		Collection<Solution> sortedSolutions = [];

		while (unsatisfiedSolutions.Count != 0)
		{
			Collection<Package> unsatisfiedPackages = unsatisfiedSolutions
				.SelectMany(s => s.Packages)
				.ToCollection();

			Collection<Solution> satisfied = unsatisfiedSolutions
				.Where(s => !s.Dependencies.IntersectBy(unsatisfiedPackages.Select(p => p.Name), p => p.Name).Any())
				.ToCollection();

			foreach (Solution solution in satisfied)
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
		PersistentState persistentState = PersistentState.Get();
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
		DirectoryPath solutionDir = solutionPath.DirectoryPath;
		DirectoryPath checkDir = solutionDir;
		do
		{
			checkDir = checkDir.Parent;

			// Stop if we've reached an empty or invalid parent (root of drive)
			if (string.IsNullOrEmpty(checkDir.ToString()))
			{
				break;
			}

			if (Directory.EnumerateFiles(checkDir, "*.sln", SearchOption.TopDirectoryOnly).Any())
			{
				return true;
			}
		}
		while (Path.IsPathFullyQualified(checkDir));

		return false;
	}
}
