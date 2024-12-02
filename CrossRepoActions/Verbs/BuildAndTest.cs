namespace ktsu.CrossRepoActions.Verbs;

using System.Collections.ObjectModel;
using CommandLine;
using ktsu.CrossRepoActions;
using ktsu.Extensions;
using ktsu.StrongPaths;

[Verb("BuildAndTest")]
internal class BuildAndTest : BaseVerb<BuildAndTest>
{
	private enum Status
	{
		InProgress,
		Error,
		Success,
	}

	internal override void Run(BuildAndTest options)
	{
		var solutions = Dotnet.DiscoverSolutions(options.Path);
		var errorSummary = new Collection<string>();

		foreach (var solution in solutions)
		{
			string cwd = Directory.GetCurrentDirectory();
			Directory.SetCurrentDirectory(solution.Path.DirectoryPath);

			OutputBuildStatus(solution.Path, Status.InProgress, 0);

			var solutionErrors = new Collection<string>();
			var projectStatuses = new Collection<string>();
			var projectErrors = new Collection<string>();

			foreach (var project in solution.Projects)
			{
				var results = Dotnet.BuildProject(project);
				solutionErrors.AddMany(results);
				if (results.Count != 0)
				{
					projectStatuses.Add($"\t❌ {project.FileName}");
					projectErrors.Add($"\t❌ {project.FileName}");
				}
				else
				{
					projectStatuses.Add($"\t✅ {project.FileName}");
				}
			}

			if (solutionErrors.Count != 0)
			{
				OutputBuildStatus(solution.Path, Status.Error, 0);
				projectStatuses.WriteItemsToConsole();
				errorSummary.Add($"❌ {solution.Name}");
				errorSummary.AddMany(projectErrors);
				solutionErrors.WriteItemsToConsole();
				continue;
			}

			var tests = Dotnet.GetTests();
			int numTests = tests.Count;
			OutputBuildStatus(solution.Path, Status.Success, numTests);
			projectStatuses.WriteItemsToConsole();

			if (numTests == 0)
			{
				continue;
			}

			var testOutput = Dotnet.RunTests();
			testOutput = testOutput
				.Where(l => l.EndsWithOrdinal("]") && (l.Contains("Passed") || l.Contains("Failed")))
				.Select(s =>
				{
					string[] parts = s.Split(' ');
					string statusString = parts[0];
					string testName = parts[1];
					string timeString = parts[2];
					var status = statusString switch
					{
						"Passed" => Status.Success,
						"Failed" => Status.Error,
						_ => Status.InProgress,
					};

					return status == Status.InProgress
						? s
						: $"\t{GetTestStatusIndicator(status)} {testName}";
				})
				.ToCollection();

			testOutput.ForEach(s =>
			{
				Console.WriteLine(s);
				if (s.Contains(GetTestStatusIndicator(Status.Error)))
				{
					string[] parts = s.Split(' ');
					errorSummary.Add($"{parts[0]} {solution.Name}.{parts[1]}");
				}
			});

			Directory.SetCurrentDirectory(cwd);
		}

		Console.WriteLine();
		errorSummary.WriteItemsToConsole();
	}

	private static string GetBuildStatusIndicator(Status status) => status switch
	{
		Status.InProgress => "🛠️",
		Status.Error => "❌",
		Status.Success => "✅",
		_ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
	};

	private static string GetTestStatusIndicator(Status status) => status switch
	{
		Status.InProgress => "🧪",
		Status.Error => "❌",
		Status.Success => "✅",
		_ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
	};

	private static string GetLineEnding(Status status) => status switch
	{
		Status.InProgress => "",
		Status.Error => "\n",
		Status.Success => "\n",
		_ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
	};

	private static void OutputBuildStatus(AbsoluteFilePath solutionFilePath, Status status, int numTests)
	{
		string test = numTests > 0 ? $" ({numTests} tests found)" : "";
		Console.Write($"\r {GetBuildStatusIndicator(status)} {System.IO.Path.GetFileName(solutionFilePath)}{test}{GetLineEnding(status)}");
	}
}
