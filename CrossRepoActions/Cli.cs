// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.CrossRepoActions;

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ktsu.RunCommand;

/// <summary>
/// The result of running an external command: its exit code plus whatever it wrote to each stream.
/// </summary>
/// <param name="ExitCode">The process exit code, where zero means success.</param>
/// <param name="Output">The raw standard output.</param>
/// <param name="Error">The raw standard error.</param>
internal sealed record CommandResult(int ExitCode, string Output, string Error)
{
	/// <summary>
	/// Gets a value indicating whether the command reported success.
	/// </summary>
	internal bool Succeeded => ExitCode == 0;

	/// <summary>
	/// Gets the standard output trimmed, which is what single-value queries want.
	/// </summary>
	internal string OutputText => Output.Trim();

	/// <summary>
	/// Gets whichever stream explains a failure, preferring standard error.
	/// </summary>
	internal string FailureText => Error.Trim().Length > 0 ? Error.Trim() : Output.Trim();

	/// <summary>
	/// Gets both streams as trimmed, non-empty lines with standard output first.
	/// </summary>
	/// <remarks>
	/// Merging the streams is deliberate. This replaced a PowerShell host whose narrowest stream
	/// selection was already <c>Output | Error</c>, so every caller was written against combined
	/// output, and the ones that matter most rely on it: <c>git fetch -v</c> and <c>git pull -v</c>
	/// write their entire progress report to standard error.
	///
	/// Standard output comes first rather than being interleaved by arrival, which is what the
	/// PowerShell host did. Callers that take the first line want the answer, not a warning that
	/// happened to be flushed ahead of it.
	/// </remarks>
	internal Collection<string> AllLines => ToLines(Output, Error);

	/// <summary>
	/// Gets standard output alone as trimmed, non-empty lines, for commands whose output is parsed
	/// rather than displayed and would be corrupted by diagnostics mixed in.
	/// </summary>
	internal Collection<string> OutputLines => ToLines(Output, string.Empty);

	private static Collection<string> ToLines(params string[] streams)
	{
		Collection<string> lines = [];
		foreach (string stream in streams)
		{
			if (string.IsNullOrEmpty(stream))
			{
				continue;
			}

			foreach (string line in stream.Split('\n'))
			{
				string trimmed = line.Trim();
				if (trimmed.Length > 0)
				{
					lines.Add(trimmed);
				}
			}
		}

		return lines;
	}
}

/// <summary>
/// Runs external commands.
/// </summary>
/// <remarks>
/// This replaced hosting the PowerShell SDK, which cost a large dependency for what amounts to
/// starting a process. For git specifically it also matters that the command line is the only thing
/// that runs Git LFS: the clean filter that turns a tracked binary into a pointer, and the pre-push
/// hook that uploads the object it points at, are features of the git command rather than of any
/// library bound to libgit2.
/// </remarks>
internal static class Cli
{
	/// <summary>
	/// Runs a command with the given arguments, each passed separately so paths need no quoting.
	/// </summary>
	/// <param name="fileName">The executable to run.</param>
	/// <param name="arguments">The arguments to pass.</param>
	/// <param name="cancellationToken">A token that terminates the process when cancelled.</param>
	/// <returns>The exit code and captured output.</returns>
	internal static async Task<CommandResult> RunAsync(string fileName, IEnumerable<string> arguments, CancellationToken cancellationToken = default)
	{
		StringBuilder output = new();
		StringBuilder error = new();

		// The raw handler is deliberate: the line-splitting handler drops a trailing fragment that
		// was never newline terminated, and command line tools do not always terminate their final
		// line. Accumulate whole chunks and split once at the end.
		OutputHandler handler = new(
			onStandardOutput: data => output.Append(data),
			onStandardError: data => error.Append(data));

		int exitCode = await RunCommand.ExecuteAsync(fileName, arguments, handler, cancellationToken).ConfigureAwait(false);

		return new CommandResult(exitCode, output.ToString(), error.ToString());
	}

	/// <summary>
	/// Runs a command and waits for it, which is what the verbs and their parallel loops expect.
	/// </summary>
	/// <param name="fileName">The executable to run.</param>
	/// <param name="arguments">The arguments to pass.</param>
	/// <returns>The exit code and captured output.</returns>
	internal static CommandResult Run(string fileName, params string[] arguments)
	{
		Ensure.NotNull(arguments);

		StringBuilder output = new();
		StringBuilder error = new();

		OutputHandler handler = new(
			onStandardOutput: data => output.Append(data),
			onStandardError: data => error.Append(data));

		int exitCode = RunCommand.Execute(fileName, arguments, handler);

		return new CommandResult(exitCode, output.ToString(), error.ToString());
	}
}
