namespace ktsu.CrossRepoActions.Verbs;

using System.Collections.ObjectModel;
using CommandLine;
using ktsu.CrossRepoActions;
using ktsu.Extensions;
using ktsu.StrongPaths;

[Verb("BuildAndTest", isDefault: true)]
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
		var sortedSolutions = Dotnet.SortSolutionsByDependencies(solutions);
		var packages = solutions.SelectMany(s => s.Packages);
		//sortedSolutions.ForEach(s => Console.WriteLine($"{s.Name} ({string.Join(", ", packages.IntersectBy(s.Dependencies.Select(p => p.Name), p => p.Name).Select(p => p.Name))})"));

		var errorSummary = new Collection<string>();

		foreach (var solution in sortedSolutions)
		{
			string cwd = Directory.GetCurrentDirectory();
			Directory.SetCurrentDirectory(solution.Path.DirectoryPath);

			OutputBuildStatus(solution.Path, Status.InProgress, 0);

			var buildErrors = Dotnet.BuildAndReturnErrors();
			if (buildErrors.Any())
			{
				OutputBuildStatus(solution.Path, Status.Error, 0);
				errorSummary.Add($"❌ {solution.Name}");
				continue;
			}

			var tests = Dotnet.GetTests();
			int numTests = tests.Count();
			OutputBuildStatus(solution.Path, Status.Success, numTests);

			if (numTests == 0)
			{
				continue;
			}

			var testOutput = Dotnet.RunTests();
			testOutput = testOutput
				.Where(l => l.EndsWithOrdinal("]"))
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
					return $"\t{GetTestStatusIndicator(status)} {testName}";
				});

			testOutput.ForEach(s =>
			{
				Console.WriteLine(s);
				if (s.Contains(GetTestStatusIndicator(Status.Error)))
				{
					string[] parts = s.Split(' ');
					errorSummary.Add($"{parts[0]} {solution.Name}{parts[1]}");
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

	private static void OutputBuildStatus(AbsoluteFilePath solutionFilePath, Status status, int numTests) =>
		Console.Write($"\r {GetBuildStatusIndicator(status)} {System.IO.Path.GetFileName(solutionFilePath)} ({numTests} tests found){GetLineEnding(status)}");

	internal static void HideCursor() => Console.Write("\u001b[?25l");
	internal static void ShowCursor() => Console.Write("\u001b[?25h");
	internal static void ClearLine() => Console.Write("\u001b[2K");
	internal static void MoveCursorUp(int lines = 1) => Console.Write($"\u001b[{lines}A");
	internal static void MoveCursorDown(int lines = 1) => Console.Write($"\u001b[{lines}B");
}
