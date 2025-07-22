namespace ktsu.CrossRepoActions.Verbs;

using System.Collections.Concurrent;
using System.Collections.ObjectModel;

using CommandLine;

using ktsu.Extensions;

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
					foreach (var project in solution.Projects)
					{
						var solutionDir = solution.Path.DirectoryPath;
						bool isProjectFileModified = Git.Status(solutionDir, project).Any();
						bool canCommit = !isProjectFileModified;
						var outdatedPackages = Dotnet.GetOutdatedProjectDependencies(project);
						var results = Dotnet.UpdatePackages(project, outdatedPackages);
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
}
