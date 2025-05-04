// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CrossRepoActions;

using System;
using System.Collections.ObjectModel;
using System.Management.Automation;

using ktsu.Extensions;

[Flags]
internal enum PowershellStreams
{
	Verbose = 1 << 0,
	Error = 1 << 1,
	Output = 1 << 2,
	Debug = 1 << 3,
	Warning = 1 << 4,
	Information = 1 << 5,
	Progress = 1 << 6,
	Default = Output | Error,
	All = Verbose | Error | Output | Debug | Warning | Information | Progress
}

internal static class PowershellExtensions
{
	internal static Collection<string> InvokeAndReturnOutput(this PowerShell ps, PowershellStreams streams = PowershellStreams.Default)
	{
		using var input = new PSDataCollection<PSObject>();
		input.Complete();

		var collectedOutput = new Collection<string>();

		using var stdOutput = new PSDataCollection<PSObject>();
		if (streams.HasFlag(PowershellStreams.Output))
		{
			stdOutput.DataAdded += (s, e) =>
			{
				if (s is PSDataCollection<PSObject> data)
				{
					var newRecord = data[e.Index];
					collectedOutput.Add(newRecord.ToString());
				}
			};
		}

		if (streams.HasFlag(PowershellStreams.Verbose))
		{
			var verboseOutput = new PSDataCollection<VerboseRecord>();
			verboseOutput.DataAdded += (s, e) =>
			{
				if (s is PSDataCollection<VerboseRecord> data)
				{
					var newRecord = data[e.Index];
					collectedOutput.Add(newRecord.Message);
				}
			};
			ps.Streams.Verbose = verboseOutput;
		}

		if (streams.HasFlag(PowershellStreams.Error))
		{
			var errorOutput = new PSDataCollection<ErrorRecord>();
			errorOutput.DataAdded += (s, e) =>
			{
				if (s is PSDataCollection<ErrorRecord> data)
				{
					var newRecord = data[e.Index];
					collectedOutput.Add(newRecord.ToString());
				}
			};
			ps.Streams.Error = errorOutput;
		}

		if (streams.HasFlag(PowershellStreams.Warning))
		{
			var warningOutput = new PSDataCollection<WarningRecord>();
			warningOutput.DataAdded += (s, e) =>
			{
				if (s is PSDataCollection<WarningRecord> data)
				{
					var newRecord = data[e.Index];
					collectedOutput.Add(newRecord.Message);
				}
			};
			ps.Streams.Warning = warningOutput;
		}

		if (streams.HasFlag(PowershellStreams.Information))
		{
			var informationOutput = new PSDataCollection<InformationRecord>();
			informationOutput.DataAdded += (s, e) =>
			{
				if (s is PSDataCollection<InformationRecord> data)
				{
					var newRecord = data[e.Index];
					var dataString = newRecord.MessageData?.ToString();
					if (dataString is not null)
					{
						collectedOutput.Add(dataString);
					}
				}
			};
			ps.Streams.Information = informationOutput;
		}

		if (streams.HasFlag(PowershellStreams.Progress))
		{
			var progressOutput = new PSDataCollection<ProgressRecord>();
			progressOutput.DataAdded += (s, e) =>
			{
				if (s is PSDataCollection<ProgressRecord> data)
				{
					var newRecord = data[e.Index];
					collectedOutput.Add(newRecord.StatusDescription);
				}
			};
			ps.Streams.Progress = progressOutput;
		}

		if (streams.HasFlag(PowershellStreams.Debug))
		{
			var debugOutput = new PSDataCollection<DebugRecord>();
			debugOutput.DataAdded += (s, e) =>
			{
				if (s is PSDataCollection<DebugRecord> data)
				{
					var newRecord = data[e.Index];
					collectedOutput.Add(newRecord.Message);
				}
			};
			ps.Streams.Debug = debugOutput;
		}

		ps.Invoke(input, stdOutput);

		return collectedOutput.Select(s => s.Trim()).ToCollection();
	}
}
