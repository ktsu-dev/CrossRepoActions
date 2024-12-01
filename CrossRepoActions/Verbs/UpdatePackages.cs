namespace ktsu.CrossRepoActions.Verbs;

using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using CommandLine;
using ktsu.Extensions;

[Verb("UpdatePackages")]
internal class UpdatePackages : BaseVerb<UpdatePackages>
{
	private static object ConsoleLock { get; } = new();
	internal override void Run(UpdatePackages options)
	{
		var errorSummary = new ConcurrentBag<string>();
		var solutions = Dotnet.DiscoverSolutions(options.Path);
		var sortedSolutions = Dotnet.SortSolutionsByDependencies(solutions);

		_ = Parallel.ForEach(sortedSolutions, solution =>
		{
			foreach (var project in solution.Projects)
			{
				var results = Dotnet.UpdatePackages(project);
				var upToDate = new Collection<Package>();
				var updated = new Collection<Package>();
				var errored = new Collection<Package>();
				var errorLines = new Collection<string>();
				foreach (var dependency in solution.Dependencies)
				{
					var dependencyErrors = results.Where(s => s.Contains($"{dependency.Name}") && s.Contains("error", StringComparison.InvariantCultureIgnoreCase) && !s.Contains("imported file", StringComparison.InvariantCultureIgnoreCase));
					if (dependencyErrors.Any())
					{
						errorLines.AddMany(dependencyErrors);
						errored.Add(dependency);
						continue;
					}

					bool isUpToDate = results.Any(s => s.Contains($"'{dependency.Name}' version '{dependency.Version}' updated", StringComparison.InvariantCultureIgnoreCase));
					if (isUpToDate)
					{
						upToDate.Add(dependency);
						continue;
					}

					bool wasUpdated = results.Any(s => s.Contains($"'{dependency.Name}' version", StringComparison.InvariantCultureIgnoreCase) && s.Contains("updated in file", StringComparison.InvariantCultureIgnoreCase) && !s.Contains($"version '{dependency.Version}'", StringComparison.InvariantCultureIgnoreCase));
					if (wasUpdated)
					{
						updated.Add(dependency);
						continue;
					}
				}

				lock (ConsoleLock)
				{
					if (errored.Count != 0)
					{
						string error = $"❌ {project.FileName}";
						errorSummary.Add(error);
						Console.WriteLine(error);
						errorLines.WriteItemsToConsole();
					}
					else if (updated.Count != 0)
					{
						Console.WriteLine($"🚀 {project.FileName}");
					}
					else
					{
						Console.WriteLine($"✅ {project.FileName}");
					}
					upToDate.Select(p => $"\t✅ {p.Name}").WriteItemsToConsole();
					updated.Select(p => $"\t🚀 {p.Name}").WriteItemsToConsole();
					errored.Select(p => $"\t❌ {p.Name}").WriteItemsToConsole();
				}
			}
		});

		if (!errorSummary.IsEmpty)
		{
			Console.WriteLine();
			Console.WriteLine("Failed to update:");
			Console.WriteLine("-----------------");
			errorSummary.WriteItemsToConsole();
		}
	}
}
