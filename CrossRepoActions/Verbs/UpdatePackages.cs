namespace ktsu.CrossRepoActions.Verbs;

using System.Collections.Concurrent;
using System.Collections.ObjectModel;

using CommandLine;

using ktsu.Extensions;
using ktsu.StrongPaths;

[Verb("UpdatePackages")]
internal class UpdatePackages : BaseVerb<UpdatePackages>
{
	private static object ConsoleLock { get; } = new();
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Printing unhandled exceptions")]
	internal override void Run(UpdatePackages options)
	{
		while (true)
		{
			ConcurrentBag<string> errorSummary = [];
			var solutions = Dotnet.DiscoverSolutions(options.Path);

			_ = Parallel.ForEach(solutions, new()
			{
				MaxDegreeOfParallelism = Program.MaxParallelism,
			},
			solution =>
			{
				try
				{
					// Check if this solution uses central package management
					bool usesCentralPackageManagement = Dotnet.UsesCentralPackageManagement(solution.Path);
					var solutionDir = solution.Path.DirectoryPath;

					if (usesCentralPackageManagement)
					{
						// Handle central package management at solution level
						var outdatedPackages = Dotnet.GetOutdatedCentralPackageDependencies(solution.Path);
						if (outdatedPackages.Count > 0)
						{
							var directoryPackagesPath = Dotnet.GetDirectoryPackagesPath(solution.Path);
							bool isDirectoryPackagesModified = directoryPackagesPath != null && Git.Status(solutionDir, directoryPackagesPath).Any();
							bool canCommit = !isDirectoryPackagesModified;

							var results = Dotnet.UpdatePackagesWithCentralManagement(solution.Path, outdatedPackages);
							ProcessCentralPackageResults(solution, results, outdatedPackages, canCommit, solutionDir, directoryPackagesPath, errorSummary);
						}
					}
					else
					{
						// Handle traditional per-project package management
						foreach (var project in solution.Projects)
						{
							bool isProjectFileModified = Git.Status(solutionDir, project).Any();
							bool canCommit = !isProjectFileModified;
							var outdatedPackages = Dotnet.GetOutdatedProjectDependencies(project);
							var results = Dotnet.UpdatePackages(project, outdatedPackages);
							ProcessProjectPackageResults(project, results, outdatedPackages, canCommit, solutionDir, errorSummary);
						}
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"❌ {solution.Name}\n{ex.Message}");
					errorSummary.Add($"{solution.Name}: {ex.Message}");
				}
			});

			if (errorSummary.IsEmpty)
			{
				Console.WriteLine();
				Console.WriteLine("All packages updated successfully!");
			}
			else
			{
				Console.WriteLine();
				Console.WriteLine("Failed to update:");
				Console.WriteLine("-----------------");
				errorSummary.WriteItemsToConsole();
			}

			//Thread.Sleep(1000 * 60 * 5);
		}
	}

	private static void ProcessCentralPackageResults(Solution solution, Collection<string> results, Collection<Package> outdatedPackages, bool canCommit, AbsoluteDirectoryPath solutionDir, AbsoluteFilePath? directoryPackagesPath, ConcurrentBag<string> errorSummary)
	{
		var upToDate = new Collection<Package>();
		var updated = new Collection<Package>();
		var errored = new Collection<Package>();

		foreach (var package in outdatedPackages)
		{
			var packageErrors = results.Where(s => s.Contains($"{package.Name}") && s.Contains("error", StringComparison.InvariantCultureIgnoreCase));
			if (packageErrors.Any())
			{
				errored.Add(package);
				continue;
			}

			bool wasUpdated = results.Any(s => s.Contains($"Updated {package.Name}"));
			if (wasUpdated)
			{
				updated.Add(package);
			}
			else
			{
				upToDate.Add(package);
			}
		}

		string solutionStatus = $"✅ {solution.Name} (Central Package Management)";
		if (errored.Count != 0)
		{
			string error = $"❌ {solution.Name} (Central Package Management)";
			errorSummary.Add(error);
			solutionStatus = error;
		}
		else if (updated.Count != 0)
		{
			solutionStatus = $"🚀 {solution.Name} (Central Package Management)";
			if (canCommit && directoryPackagesPath != null)
			{
				Git.Unstage(solutionDir);
				Git.Pull(solutionDir);
				Git.Add(solutionDir, directoryPackagesPath);
				Git.Commit(solutionDir, $"Updated central package versions in {directoryPackagesPath.FileName}");
				Git.Push(solutionDir);
			}
		}

		lock (ConsoleLock)
		{
			Console.WriteLine(solutionStatus);
			upToDate.Select(p => $"\t✅ {p.Name}").WriteItemsToConsole();
			updated.Select(p => $"\t🚀 {p.Name}").WriteItemsToConsole();
			errored.Select(p => $"\t❌ {p.Name}").WriteItemsToConsole();
		}
	}

	private static void ProcessProjectPackageResults(AbsoluteFilePath project, Collection<string> results, Collection<Package> outdatedPackages, bool canCommit, AbsoluteDirectoryPath solutionDir, ConcurrentBag<string> errorSummary)
	{
		var upToDate = new Collection<Package>();
		var updated = new Collection<Package>();
		var errored = new Collection<Package>();
		var errorLines = new Collection<string>();

		foreach (var package in outdatedPackages)
		{
			var packageErrors = results.Where(s => s.Contains($"{package.Name}") && s.Contains("error", StringComparison.InvariantCultureIgnoreCase) && !s.Contains("imported file", StringComparison.InvariantCultureIgnoreCase));
			if (packageErrors.Any())
			{
				errorLines.AddMany(packageErrors);
				errored.Add(package);
				continue;
			}

			bool isUpToDate = results.Any(s => s.Contains($"'{package.Name}' version '{package.Version}' updated", StringComparison.InvariantCultureIgnoreCase));
			if (isUpToDate)
			{
				upToDate.Add(package);
				continue;
			}

			bool wasUpdated = results.Any(s => s.Contains($"'{package.Name}' version", StringComparison.InvariantCultureIgnoreCase) && s.Contains("updated in file", StringComparison.InvariantCultureIgnoreCase) && !s.Contains($"version '{package.Version}'", StringComparison.InvariantCultureIgnoreCase));
			if (wasUpdated)
			{
				updated.Add(package);
				continue;
			}
		}

		string projectStatus = $"✅ {project.FileName}";
		if (errored.Count != 0)
		{
			string error = $"❌ {project.FileName}";
			errorSummary.Add(error);
			projectStatus = error;
		}
		else if (updated.Count != 0)
		{
			projectStatus = $"🚀 {project.FileName}";
			if (canCommit)
			{
				Git.Unstage(solutionDir);
				Git.Pull(solutionDir);
				Git.Add(solutionDir, project);
				Git.Commit(solutionDir, $"Updated packages in {project.FileName}");
				Git.Push(solutionDir);
			}
		}

		lock (ConsoleLock)
		{
			Console.WriteLine(projectStatus);
			upToDate.Select(p => $"\t✅ {p.Name}").WriteItemsToConsole();
			updated.Select(p => $"\t🚀 {p.Name}").WriteItemsToConsole();
			errored.Select(p => $"\t❌ {p.Name}").WriteItemsToConsole();
		}
	}
}
