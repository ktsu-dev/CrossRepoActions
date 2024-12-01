namespace ktsu.CrossRepoActions;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using ktsu.StrongPaths;
using ktsu.Extensions;
using System.Collections.Concurrent;

internal static class Dotnet
{
	internal static IEnumerable<string> BuildAndReturnErrors()
	{
		var results = PowerShell.Create()
			.AddCommand("dotnet")
			.AddArgument("build")
			.AddArgument("--nologo")
			.Invoke()
			.Select(o => o.ToString());

		return GetErrors(results);
	}

	internal static IEnumerable<string> RunTests()
	{
		var ps = PowerShell.Create()
			.AddCommand("dotnet")
			.AddArgument("vstest")
			.AddArgument("**/bin/**/*Test.dll")
			.AddArgument("/logger:console;verbosity=normal")
			.AddArgument("--nologo");

		return ps.InvokeAndReturnOutput(PowershellStreams.All);
	}

	internal static IEnumerable<string> RunSingleTestAndReturnErrors(string testName)
	{
		var results = PowerShell.Create()
			.AddCommand("dotnet")
			.AddArgument("test")
			.AddArgument("--nologo")
			.AddArgument("--no-build")
			.AddArgument("--filter")
			.AddArgument(testName)
			.Invoke()
			.Select(o => o.ToString());

		return GetErrors(results);
	}

	internal static IEnumerable<string> GetTests()
	{
		var results = PowerShell.Create()
			.AddCommand("dotnet")
			.AddArgument("vstest")
			.AddArgument("--ListTests")
			.AddArgument("--nologo")
			.AddArgument("**/bin/**/*Test.dll")
			.Invoke();

		var stringResults = results
			.Select(o => o.ToString())
			.Where(r => r != r.Trim());

		return stringResults;
	}

	internal static IEnumerable<string> GetProjects(AbsoluteFilePath solutionFile)
	{
		var results = PowerShell.Create()
			.AddCommand("dotnet")
			.AddArgument("sln")
			.AddArgument(Path.GetFullPath(solutionFile))
			.AddArgument("list")
			.Invoke();

		var stringResults = results
			.Select(o => o.ToString().Trim())
			.Where(r => r.EndsWithOrdinal(".csproj"));

		return stringResults;
	}

	internal static IEnumerable<Package> GetSolutionDependencies(AbsoluteFilePath solutionFile)
	{
		var results = PowerShell.Create()
			.AddCommand("dotnet")
			.AddArgument("list")
			.AddArgument(Path.GetFullPath(solutionFile))
			.AddArgument("package")
			.AddArgument("--include-transitive")
			.Invoke();

		var stringResults = results
			.Select(o => o.ToString().Trim())
			.Where(r => r.StartsWithOrdinal(">"));

		var dependencies = stringResults
			.Select(r =>
			{
				string[] parts = r.Split(' ');
				return new Package()
				{
					Name = parts[1],
					Version = parts.Last(),
				};
			});


		return dependencies;
	}

	internal static string GetAssemblyName(AbsoluteFilePath projectFile)
	{
		var results = PowerShell.Create()
			.AddCommand("dotnet")
			.AddArgument("msbuild")
			.AddArgument(Path.GetFullPath(projectFile))
			.AddArgument("-getProperty:AssemblyName")
			.Invoke();

		var stringResults = results
			.Select(o => o.ToString().Trim());

		return stringResults.First();
	}

	internal static string GetAssemblyVersion(AbsoluteFilePath projectFile)
	{
		var results = PowerShell.Create()
			.AddCommand("dotnet")
			.AddArgument("msbuild")
			.AddArgument(Path.GetFullPath(projectFile))
			.AddArgument("-getProperty:Version")
			.Invoke();

		var stringResults = results
			.Select(o => o.ToString().Trim());

		return stringResults.First();
	}

	internal static Package GetAssemblyPackage(AbsoluteFilePath projectFile)
	{
		return new Package()
		{
			Name = GetAssemblyName(Path.GetFullPath(projectFile).As<AbsoluteFilePath>()),
			Version = GetAssemblyVersion(Path.GetFullPath(projectFile).As<AbsoluteFilePath>())
		};
	}

	internal static IEnumerable<string> GetErrors(IEnumerable<string> strings) =>
		strings.Where(r => (r.Contains("error") || r.Contains("failed"))
						&& !(r.Contains("passed") || r.Contains("0 Error")));

	private static object ConsoleLock { get; } = new();
	internal static Collection<Solution> DiscoverSolutionDependencies(IEnumerable<AbsoluteFilePath> solutionFiles)
	{
		var solutionFileCollection = solutionFiles.ToCollection();
		var solutions = new ConcurrentBag<Solution>();
		_ = Parallel.ForEach(solutionFileCollection, solutionFile =>
		{
			var projects = GetProjects(solutionFile).ToCollection();
			var packages = projects.Select(p => GetAssemblyPackage(solutionFile.DirectoryPath / p.As<RelativeFilePath>())).ToCollection();
			var dependencies = GetSolutionDependencies(solutionFile).ToCollection();
			lock (ConsoleLock)
			{
				Console.WriteLine($"\nFound {solutionFile} with:");
				Console.WriteLine("  Packages:");
				packages.ForEach(p => Console.WriteLine($"    {p.Name} {p.Version}"));
				Console.WriteLine("  Dependencies:");
				dependencies.ForEach(d => Console.WriteLine($"    {d.Name} {d.Version}"));
			}
			var solution = new Solution()
			{
				Name = Path.GetFileNameWithoutExtension(solutionFile.FileName),
				Path = solutionFile,
				Packages = packages.ToCollection(),
				Dependencies = dependencies.ToCollection(),
			};
			solutions.Add(solution);
		});

		Console.WriteLine();

		return solutions.ToCollection();
	}

	internal static Collection<Solution> SortSolutionsByDependencies(ICollection<Solution> solutions)
	{
		var unsatisfiedSolutions = solutions.ToCollection();
		var sortedSolutions = new Collection<Solution>();

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

	internal static IEnumerable<AbsoluteFilePath> DiscoverSolutionFiles(AbsoluteDirectoryPath root)
	{
		return Directory.EnumerateFiles(root, "*.sln", SearchOption.AllDirectories)
		.Select(p => p.As<AbsoluteFilePath>())
		.Where(p => !IsSolutionNested(p));
	}

	internal static Collection<Solution> DiscoverSolutions(AbsoluteDirectoryPath root)
	{
		Console.WriteLine($"Discovering solutions in {root}");
		return DiscoverSolutionDependencies(DiscoverSolutionFiles(root));
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
