namespace ktsu.CrossRepoActions;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using ktsu.StrongPaths;
using ktsu.Extensions;
using System.Collections.Concurrent;
using NuGet.Versioning;
using DustInTheWind.ConsoleTools.Controls.Spinners;

internal static class Dotnet
{
	internal static Collection<string> BuildSolution()
	{
		var results = PowerShell.Create()
			.AddCommand("dotnet")
			.AddArgument("build")
			.AddArgument("--nologo")
			.InvokeAndReturnOutput();

		return GetErrors(results);
	}

	internal static Collection<string> BuildProject(AbsoluteFilePath projectFile)
	{
		var results = PowerShell.Create()
			.AddCommand("dotnet")
			.AddArgument("build")
			.AddArgument("--nologo")
			.AddArgument(projectFile.ToString())
			.InvokeAndReturnOutput();

		return GetErrors(results);
	}

	internal static Collection<string> RunTests()
	{
		var ps = PowerShell.Create()
			.AddCommand("dotnet")
			.AddArgument("vstest")
			.AddArgument("**/bin/**/*Test.dll")
			.AddArgument("/logger:console;verbosity=normal")
			.AddArgument("--nologo");

		return ps.InvokeAndReturnOutput(PowershellStreams.All);
	}

	internal static Collection<string> GetTests()
	{
		var results = PowerShell.Create()
			.AddCommand("dotnet")
			.AddArgument("vstest")
			.AddArgument("--ListTests")
			.AddArgument("--nologo")
			.AddArgument("**/bin/**/*Test.dll")
			.InvokeAndReturnOutput();

		var stringResults = results
			.Where(r => !r.StartsWith("The following") && !r.StartsWith("No test source"))
			.ToCollection();

		return stringResults;
	}

	internal static Collection<string> GetProjects(AbsoluteFilePath solutionFile)
	{
		var results = PowerShell.Create()
			.AddCommand("dotnet")
			.AddArgument("sln")
			.AddArgument(solutionFile.ToString())
			.AddArgument("list")
			.InvokeAndReturnOutput();

		var stringResults = results
			.Where(r => r.EndsWithOrdinal(".csproj"))
			.ToCollection();

		return stringResults;
	}

	internal static Collection<Package> GetSolutionDependencies(AbsoluteFilePath solutionFile)
	{
		var results = PowerShell.Create()
			.AddCommand("dotnet")
			.AddArgument("list")
			.AddArgument(solutionFile.ToString())
			.AddArgument("package")
			.AddArgument("--include-transitive")
			.InvokeAndReturnOutput();

		var stringResults = results
			.Where(r => r.StartsWithOrdinal(">"))
			.ToCollection();

		var dependencies = stringResults
			.Select(r =>
			{
				string[] parts = r.Split(' ');
				return new Package()
				{
					Name = parts[1],
					Version = NuGetVersion.Parse(parts.Last()),
				};
			})
			.ToCollection();

		return dependencies;
	}

	internal static Collection<Package> GetProjectDependencies(AbsoluteFilePath projectFile)
	{
		var results = PowerShell.Create()
			.AddCommand("dotnet")
			.AddArgument("list")
			.AddArgument(projectFile.ToString())
			.AddArgument("package")
			.InvokeAndReturnOutput();

		var stringResults = results
			.Where(r => r.StartsWithOrdinal(">"))
			.ToCollection();

		var dependencies = stringResults
			.Select(r =>
			{
				string[] parts = r.Split(' ');
				return new Package()
				{
					Name = parts[1],
					Version = NuGetVersion.Parse(parts.Last()),
				};
			})
			.ToCollection();

		return dependencies;
	}

	internal static Collection<string> UpdatePackages(AbsoluteFilePath projectFile)
	{
		var output = new Collection<string>();
		var dependencies = GetProjectDependencies(projectFile);
		foreach (var dependency in dependencies)
		{
			var ps = PowerShell.Create()
				.AddCommand("dotnet")
				.AddArgument("add")
				.AddArgument(projectFile.ToString())
				.AddArgument("package")
				.AddArgument(dependency.Name);

			bool isPreRelease = dependency.Version.IsPrerelease;
			if (isPreRelease)
			{
				ps = ps.AddArgument("--prerelease");
			}

			output.AddMany(ps.InvokeAndReturnOutput());
		}

		return output;
	}

	internal static string GetProjectAssemblyName(AbsoluteFilePath projectFile)
	{
		var results = PowerShell.Create()
			.AddCommand("dotnet")
			.AddArgument("msbuild")
			.AddArgument(projectFile.ToString())
			.AddArgument("-getProperty:AssemblyName")
			.InvokeAndReturnOutput();

		return results.First();
	}

	internal static NuGetVersion GetProjectVersion(AbsoluteFilePath projectFile)
	{
		var results = PowerShell.Create()
			.AddCommand("dotnet")
			.AddArgument("msbuild")
			.AddArgument(projectFile.ToString())
			.AddArgument("-getProperty:Version")
			.InvokeAndReturnOutput();

		return NuGetVersion.Parse(results.First());
	}

	internal static bool IsProjectPackable(AbsoluteFilePath projectFile)
	{
		var results = PowerShell.Create()
			.AddCommand("dotnet")
			.AddArgument("msbuild")
			.AddArgument(projectFile.ToString())
			.AddArgument("-getProperty:IsPackable")
			.InvokeAndReturnOutput();

		return bool.Parse(results.First());
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
		var solutionFileCollection = solutionFiles.ToCollection();
		var solutions = new ConcurrentBag<Solution>();

		var progressBar = new ProgressBar();
		progressBar.Display();

		_ = Parallel.ForEach(solutionFileCollection, solutionFile =>
		{
			var projects = GetProjects(solutionFile)
				.Select(p => solutionFile.DirectoryPath / p.As<RelativeFilePath>())
				.ToCollection();

			var packages = projects
				.Where(p => IsProjectPackable(p))
				.Select(p => GetProjectPackage(p))
				.ToCollection();

			var dependencies = GetSolutionDependencies(solutionFile);

			var solution = new Solution()
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

	internal static Collection<AbsoluteFilePath> DiscoverSolutionFiles(AbsoluteDirectoryPath root)
	{
		return Directory.EnumerateFiles(root, "*.sln", SearchOption.AllDirectories)
		.Select(p => p.As<AbsoluteFilePath>())
		.Where(p => !IsSolutionNested(p))
		.ToCollection();
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

	internal static void ClearLine() => Console.Write("\r\u001b[2K");
}
