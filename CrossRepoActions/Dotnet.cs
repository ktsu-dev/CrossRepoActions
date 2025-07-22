namespace ktsu.CrossRepoActions;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Nodes;

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
}
