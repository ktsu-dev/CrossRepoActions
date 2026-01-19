// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CrossRepoActions;

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Text.Json.Nodes;

using DustInTheWind.ConsoleTools.Controls.Spinners;

using ktsu.Extensions;
using ktsu.Semantics.Paths;
using ktsu.Semantics.Strings;

using NuGet.Versioning;

internal static class Dotnet
{
	internal static Collection<string> BuildSolution()
	{
		using PowerShell ps = PowerShell.Create();
		Collection<string> results = ps
			.AddCommand("dotnet")
			.AddArgument("build")
			.AddArgument("--nologo")
			.InvokeAndReturnOutput();

		return GetErrors(results);
	}

	internal static Collection<string> BuildProject(AbsoluteFilePath projectFile)
	{
		using PowerShell ps = PowerShell.Create();
		Collection<string> results = ps
			.AddCommand("dotnet")
			.AddArgument("build")
			.AddArgument("--nologo")
			.AddArgument(projectFile.ToString())
			.InvokeAndReturnOutput();

		return GetErrors(results);
	}

	internal static Collection<string> RunTests()
	{
		using PowerShell ps = PowerShell.Create();
		Collection<string> results = ps
			.AddCommand("dotnet")
			.AddArgument("vstest")
			.AddArgument("**/bin/**/*Test.dll")
			.AddArgument("/logger:console;verbosity=normal")
			.AddArgument("--nologo")
			.InvokeAndReturnOutput(PowershellStreams.All);

		return results;
	}

	internal static Collection<string> GetTests()
	{
		using PowerShell ps = PowerShell.Create();
		Collection<string> results = ps
			.AddCommand("dotnet")
			.AddArgument("vstest")
			.AddArgument("--ListTests")
			.AddArgument("--nologo")
			.AddArgument("**/bin/**/*Test.dll")
			.InvokeAndReturnOutput();

		Collection<string> stringResults = results
			.Where(r => !r.StartsWith("The following") && !r.StartsWith("No test source"))
			.ToCollection();

		return stringResults;
	}

	internal static Collection<string> GetProjects(AbsoluteFilePath solutionFile)
	{
		using PowerShell ps = PowerShell.Create();
		Collection<string> results = ps
			.AddCommand("dotnet")
			.AddArgument("sln")
			.AddArgument(solutionFile.ToString())
			.AddArgument("list")
			.InvokeAndReturnOutput();

		Collection<string> stringResults = results
			.Where(r => r.EndsWithOrdinal(".csproj"))
			.ToCollection();

		return stringResults;
	}

	internal static Collection<Package> GetSolutionDependencies(AbsoluteFilePath solutionFile)
	{
		using PowerShell ps = PowerShell.Create();
		Collection<string> results = ps
			.AddCommand("dotnet")
			.AddArgument("list")
			.AddArgument(solutionFile.ToString())
			.AddArgument("package")
			.AddArgument("--include-transitive")
			.InvokeAndReturnOutput();

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
		using PowerShell ps = PowerShell.Create();
		Collection<string> jsonResult = ps
			.AddCommand("dotnet")
			.AddArgument("list")
			.AddArgument(projectFile.ToString())
			.AddArgument("package")
			.AddArgument("--outdated")
			.AddArgument("--format=json")
			.InvokeAndReturnOutput();

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

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "False positive: we're using a using declaration")]
	internal static Collection<string> UpdatePackages(AbsoluteFilePath projectFile, IEnumerable<Package> packages)
	{
		Collection<string> output = [];
		foreach (Package package in packages)
		{
			bool isPreRelease = NuGetVersion.Parse(package.Version).IsPrerelease;
			using PowerShell ps = PowerShell.Create();
			if (isPreRelease)
			{
				Collection<string> results = ps
					.AddCommand("dotnet")
					.AddArgument("add")
					.AddArgument(projectFile.ToString())
					.AddArgument("package")
					.AddArgument(package.Name)
					.AddArgument("--prerelease")
					.InvokeAndReturnOutput();
				output.AddFrom(results);
			}
			else
			{
				Collection<string> results = ps
				.AddCommand("dotnet")
				.AddArgument("add")
				.AddArgument(projectFile.ToString())
				.AddArgument("package")
				.AddArgument(package.Name)
				.InvokeAndReturnOutput();
				output.AddFrom(results);
			}
		}

		return output;
	}

	internal static string GetProjectAssemblyName(AbsoluteFilePath projectFile)
	{
		using PowerShell ps = PowerShell.Create();
		Collection<string> results = ps
			.AddCommand("dotnet")
			.AddArgument("msbuild")
			.AddArgument(projectFile.ToString())
			.AddArgument("-getProperty:AssemblyName")
			.InvokeAndReturnOutput();

		return results.First();
	}

	internal static string GetProjectVersion(AbsoluteFilePath projectFile)
	{
		using PowerShell ps = PowerShell.Create();
		Collection<string> results = ps
			.AddCommand("dotnet")
			.AddArgument("msbuild")
			.AddArgument(projectFile.ToString())
			.AddArgument("-getProperty:Version")
			.InvokeAndReturnOutput();

		return results.First();
	}

	internal static bool IsProjectPackable(AbsoluteFilePath projectFile)
	{
		using PowerShell ps = PowerShell.Create();
		Collection<string> results = ps
			.AddCommand("dotnet")
			.AddArgument("msbuild")
			.AddArgument(projectFile.ToString())
			.AddArgument("-getProperty:IsPackable")
			.InvokeAndReturnOutput();

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
