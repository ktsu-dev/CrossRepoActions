namespace ktsu.CrossRepoActions;
using System.Management.Automation;
using CommandLine;
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
		sortedSolutions.ForEach(s => Console.WriteLine($"{s.Name} ({string.Join(", ", packages.IntersectBy(s.Dependencies.Select(p => p.Name), p => p.Name).Select(p => p.Name))})"));

		foreach (var solution in sortedSolutions)
		{
			string cwd = Directory.GetCurrentDirectory();
			Directory.SetCurrentDirectory(solution.Path.DirectoryPath);

			OutputBuildStatus(solution.Path, Status.InProgress, 0);

			var buildErrors = Dotnet.BuildAndReturnErrors();
			if (buildErrors.Any())
			{
				OutputBuildStatus(solution.Path, Status.Error, 0);
				continue;
			}

			var tests = Dotnet.GetTests();
			int numTests = tests.Count();
			OutputBuildStatus(solution.Path, Status.Success, numTests);

			if (numTests == 0)
			{
				continue;
			}

			var testInvoker = Dotnet.MakeTestsInvoker();
			var testOutput = new PSDataCollection<PSObject>();
			testOutput.DataAdded += (s, e) =>
			{
				if (s is PSDataCollection<PSObject> data)
				{
					var newRecord = data[e.Index];
					string stringRecord = newRecord.ToString().Trim();
					if (stringRecord.EndsWithOrdinal("]"))
					{
						string[] parts = stringRecord.Split(' ');
						string statusString = parts[0];
						string testName = parts[1];
						string timeString = parts[2];
						var status = statusString switch
						{
							"Passed" => Status.Success,
							"Failed" => Status.Error,
							_ => Status.InProgress,
						};
						OutputTestStatus(testName, status);
					}
				}
			};

			testInvoker.Streams.Error.DataAdded += (s, e) =>
			{
				if (s is PSDataCollection<ErrorRecord> data)
				{
					var newRecord = data[e.Index];
					Console.WriteLine("[ERROR]" + newRecord.ToString());
				}
			};

			testInvoker.Streams.Information.DataAdded += (s, e) =>
			{
				if (s is PSDataCollection<InformationRecord> data)
				{
					var newRecord = data[e.Index];
					Console.WriteLine("[INFO]" + newRecord.ToString());
				}
			};

			testInvoker.Streams.Progress.DataAdded += (s, e) =>
			{
				if (s is PSDataCollection<ProgressRecord> data)
				{
					var newRecord = data[e.Index];
					Console.WriteLine("[PROGRESS]" + newRecord.ToString());
				}
			};

			testInvoker.Streams.Verbose.DataAdded += (s, e) =>
			{
				if (s is PSDataCollection<VerboseRecord> data)
				{
					var newRecord = data[e.Index];
					Console.WriteLine("[VERBOSE]" + newRecord.ToString());
				}
			};

			testInvoker.Streams.Warning.DataAdded += (s, e) =>
			{
				if (s is PSDataCollection<WarningRecord> data)
				{
					var newRecord = data[e.Index];
					Console.WriteLine("[WARNING]" + newRecord.ToString());
				}
			};

			var input = new PSDataCollection<PSObject>();
			input.Complete();
			var task = testInvoker.BeginInvoke(input, testOutput);
			while (!task.IsCompleted)
			{
				Thread.Sleep(1000);
			}
			testInvoker.EndInvoke(task);

			Directory.SetCurrentDirectory(cwd);
		}
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

	private static void OutputTestStatus(string testName, Status status) =>
		Console.Write($"\t{GetTestStatusIndicator(status)} {testName}\n");

	internal static void HideCursor() => Console.Write("\u001b[?25l");
	internal static void ShowCursor() => Console.Write("\u001b[?25h");
	internal static void ClearLine() => Console.Write("\u001b[2K");
	internal static void MoveCursorUp(int lines = 1) => Console.Write($"\u001b[{lines}A");
	internal static void MoveCursorDown(int lines = 1) => Console.Write($"\u001b[{lines}B");
}
