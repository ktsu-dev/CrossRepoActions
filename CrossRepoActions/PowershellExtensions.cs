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
		using PSDataCollection<PSObject> input = [];
		input.Complete();

		Collection<string> collectedOutput = [];

		using PSDataCollection<PSObject> stdOutput = [];
		if (streams.HasFlag(PowershellStreams.Output))
		{
			stdOutput.DataAdded += (s, e) =>
			{
				if (s is PSDataCollection<PSObject> data)
				{
					PSObject newRecord = data[e.Index];
					collectedOutput.Add(newRecord.ToString());
				}
			};
		}

		if (streams.HasFlag(PowershellStreams.Verbose))
		{
			PSDataCollection<VerboseRecord> verboseOutput = [];
			verboseOutput.DataAdded += (s, e) =>
			{
				if (s is PSDataCollection<VerboseRecord> data)
				{
					VerboseRecord newRecord = data[e.Index];
					collectedOutput.Add(newRecord.Message);
				}
			};
			ps.Streams.Verbose = verboseOutput;
		}

		if (streams.HasFlag(PowershellStreams.Error))
		{
			PSDataCollection<ErrorRecord> errorOutput = [];
			errorOutput.DataAdded += (s, e) =>
			{
				if (s is PSDataCollection<ErrorRecord> data)
				{
					ErrorRecord newRecord = data[e.Index];
					collectedOutput.Add(newRecord.ToString());
				}
			};
			ps.Streams.Error = errorOutput;
		}

		if (streams.HasFlag(PowershellStreams.Warning))
		{
			PSDataCollection<WarningRecord> warningOutput = [];
			warningOutput.DataAdded += (s, e) =>
			{
				if (s is PSDataCollection<WarningRecord> data)
				{
					WarningRecord newRecord = data[e.Index];
					collectedOutput.Add(newRecord.Message);
				}
			};
			ps.Streams.Warning = warningOutput;
		}

		if (streams.HasFlag(PowershellStreams.Information))
		{
			PSDataCollection<InformationRecord> informationOutput = [];
			informationOutput.DataAdded += (s, e) =>
			{
				if (s is PSDataCollection<InformationRecord> data)
				{
					InformationRecord newRecord = data[e.Index];
					string? dataString = newRecord.MessageData?.ToString();
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
			PSDataCollection<ProgressRecord> progressOutput = [];
			progressOutput.DataAdded += (s, e) =>
			{
				if (s is PSDataCollection<ProgressRecord> data)
				{
					ProgressRecord newRecord = data[e.Index];
					collectedOutput.Add(newRecord.StatusDescription);
				}
			};
			ps.Streams.Progress = progressOutput;
		}

		if (streams.HasFlag(PowershellStreams.Debug))
		{
			PSDataCollection<DebugRecord> debugOutput = [];
			debugOutput.DataAdded += (s, e) =>
			{
				if (s is PSDataCollection<DebugRecord> data)
				{
					DebugRecord newRecord = data[e.Index];
					collectedOutput.Add(newRecord.Message);
				}
			};
			ps.Streams.Debug = debugOutput;
		}

		ps.Invoke(input, stdOutput);

		return collectedOutput.Select(s => s.Trim()).ToCollection();
	}
}
