// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CrossRepoActions.Verbs;

using System.Text;

using CommandLine;

using ktsu.CrossRepoActions.Llm;

[Verb("ConfigureLlm")]
internal sealed class ConfigureLlm : BaseVerb<ConfigureLlm>
{
	internal override void Run(ConfigureLlm options)
	{
		PersistentState state = PersistentState.Get();
		LlmSettings llm = state.Llm;

		Console.WriteLine("Configure OpenAI LLM settings (press Enter to keep the current value).");

		llm.Model = PromptWithDefault("Model", llm.Model);
		llm.OrganizationId = PromptWithDefault("Organization ID (optional)", llm.OrganizationId);
		llm.MaxDiffChars = PromptIntWithDefault("Max diff characters", llm.MaxDiffChars);

		Console.Write($"API key [{MaskKey(llm.ApiKey)}]: ");
		string key = ReadSecret();
		if (!string.IsNullOrWhiteSpace(key))
		{
			llm.ApiKey = key.Trim();
		}

		state.Save();
		Console.WriteLine("Saved.");
	}

	private static string PromptWithDefault(string label, string current)
	{
		Console.Write($"{label} [{current}]: ");
		string input = Console.ReadLine() ?? "";
		return string.IsNullOrWhiteSpace(input) ? current : input.Trim();
	}

	private static int PromptIntWithDefault(string label, int current)
	{
		Console.Write($"{label} [{current}]: ");
		string input = Console.ReadLine() ?? "";
		return int.TryParse(input.Trim(), out int value) && value > 0 ? value : current;
	}

	private static string MaskKey(string key)
	{
		if (string.IsNullOrEmpty(key))
		{
			return "not set";
		}

		return key.Length <= 8 ? "****" : $"{key[..3]}…{key[^4..]}";
	}

	private static string ReadSecret()
	{
		StringBuilder sb = new();
		ConsoleKeyInfo keyInfo;
		while ((keyInfo = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
		{
			if (keyInfo.Key == ConsoleKey.Backspace)
			{
				if (sb.Length > 0)
				{
					sb.Length--;
					Console.Write("\b \b");
				}
			}
			else if (!char.IsControl(keyInfo.KeyChar))
			{
				_ = sb.Append(keyInfo.KeyChar);
				Console.Write('*');
			}
		}

		Console.WriteLine();
		return sb.ToString();
	}
}
