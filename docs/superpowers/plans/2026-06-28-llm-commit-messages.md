# Semantic Kernel LLM Integration & AI Commit Messages Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Microsoft Semantic Kernel for talking to remote LLMs (OpenAI), and a `SuggestCommits` verb that generates Conventional Commits messages for repos with uncommitted changes, then lets the user review/commit/push them.

**Architecture:** A small `ILlmService` abstraction wraps Semantic Kernel's OpenAI chat completion. A pure `CommitMessageGenerator` builds prompts from a repo's diff and cleans the model's response (unit-tested with a fake `ILlmService`). New `Git.cs` methods supply the diff and perform staging/diff-tool. The `SuggestCommits` verb orchestrates: parallel fetch → behind-check/pull → parallel generate → serial review with per-action remote re-check. A `ConfigureLlm` verb persists settings in the existing `ktsu.AppDataStorage` state.

**Tech Stack:** C# / .NET 10, ktsu.Sdk, Microsoft.SemanticKernel 1.77.0, Microsoft.PowerShell.SDK, CommandLineParser, MSTest.

## Global Constraints

- Target framework: `net10.0` (latest only; `TargetFrameworks` empty). Main app and test project both.
- Tabs for indentation; CRLF; file-scoped namespaces; usings inside namespace; braces on all control flow; explicit accessibility; no `this.`; nullable enabled; **warnings as errors**.
- Namespace root: `ktsu.CrossRepoActions.*`. App assembly: `ktsu.CrossRepoActions`. Test assembly: `ktsu.CrossRepoActions.Test`.
- Use `ktsu.Semantics.Paths` (`AbsoluteDirectoryPath`) and existing `PowershellExtensions` (`InvokeAndReturnOutput(PowershellStreams.All)`) for git calls — match the existing `Git.cs` style exactly.
- Central Package Management: versions go in `Directory.Packages.props`; csproj uses versionless `<PackageReference>`.
- Testing: MSTest, semantic asserts (no `Assert.IsTrue`/`IsFalse`); test projects target latest .NET only.
- No global warning suppressions; only targeted `[SuppressMessage]` with justification.
- Commit messages in this plan use Conventional Commits with **no** version tag (PSBuild infers the bump).
- Do not manually edit auto-generated files (VERSION.md, CHANGELOG.md, etc.).

---

### Task 1: Test project scaffold + InternalsVisibleTo

Stand up `CrossRepoActions.Test` and expose the app's internals to it, verified by a smoke test that reads an internal member.

**Files:**
- Create: `CrossRepoActions.Test/CrossRepoActions.Test.csproj`
- Create: `CrossRepoActions.Test/SmokeTest.cs`
- Create: `CrossRepoActions/AssemblyInfo.cs`
- Modify: `CrossRepoActions.sln` (register the new project)

**Interfaces:**
- Consumes: `Program.MaxParallelism` (existing internal const `= -1`).
- Produces: a working MSTest project that can see `internal` types of `ktsu.CrossRepoActions`.

- [ ] **Step 1: Create the test project file**

`CrossRepoActions.Test/CrossRepoActions.Test.csproj`:
```xml
<Project>
  <Sdk Name="MSTest.Sdk" />
  <Sdk Name="ktsu.Sdk" />

  <PropertyGroup>
    <IsTestProject>true</IsTestProject>
    <TargetFramework>net10.0</TargetFramework>
    <TargetFrameworks></TargetFrameworks>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\CrossRepoActions\CrossRepoActions.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Add InternalsVisibleTo in the app**

`CrossRepoActions/AssemblyInfo.cs`:
```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("ktsu.CrossRepoActions.Test")]
```

- [ ] **Step 3: Write the smoke test**

`CrossRepoActions.Test/SmokeTest.cs`:
```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CrossRepoActions.Test;

using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class SmokeTest
{
	[TestMethod]
	public void InternalsAreVisibleToTests()
	{
		Assert.AreEqual(-1, Program.MaxParallelism);
	}
}
```

- [ ] **Step 4: Register the test project in the solution**

Run:
```bash
cd /c/dev/ktsu-dev/CrossRepoActions && dotnet sln CrossRepoActions.sln add CrossRepoActions.Test/CrossRepoActions.Test.csproj
```
Expected: `Project ... added to the solution.`

- [ ] **Step 5: Run the test to verify it passes**

Run:
```bash
cd /c/dev/ktsu-dev/CrossRepoActions && dotnet test
```
Expected: build succeeds; 1 test passes. (If `InternalsVisibleTo` were missing, the build would fail on `Program.MaxParallelism` being inaccessible.)

- [ ] **Step 6: Commit**

```bash
git add CrossRepoActions.Test CrossRepoActions/AssemblyInfo.cs CrossRepoActions.sln
git commit -m "test: add test project with InternalsVisibleTo"
```

---

### Task 2: Semantic Kernel packages + LLM settings

Add the SK packages and the persisted `LlmSettings`.

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `CrossRepoActions/CrossRepoActions.csproj:13-29` (ItemGroup of PackageReferences)
- Create: `CrossRepoActions/Llm/LlmSettings.cs`
- Modify: `CrossRepoActions/PersistentState.cs`

**Interfaces:**
- Produces: `LlmSettings` with `string ApiKey`, `string Model` (default `"gpt-5.4-mini"`), `string OrganizationId`, `int MaxDiffChars` (default `8000`); accessible via `PersistentState.Get().Llm`.

- [ ] **Step 1: Add package versions**

In `Directory.Packages.props`, add inside the `<ItemGroup>` (after the `Microsoft.PowerShell.SDK` line):
```xml
    <PackageVersion Include="Microsoft.SemanticKernel" Version="1.77.0" />
    <PackageVersion Include="Microsoft.SemanticKernel.Connectors.OpenAI" Version="1.77.0" />
```

- [ ] **Step 2: Reference the packages in the app**

In `CrossRepoActions/CrossRepoActions.csproj`, add to the PackageReference `<ItemGroup>` (keep alphabetical near the other `Microsoft.*` entries):
```xml
    <PackageReference Include="Microsoft.SemanticKernel" />
    <PackageReference Include="Microsoft.SemanticKernel.Connectors.OpenAI" />
```

- [ ] **Step 3: Create LlmSettings**

`CrossRepoActions/Llm/LlmSettings.cs`:
```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CrossRepoActions.Llm;

internal sealed class LlmSettings
{
	public string ApiKey { get; set; } = "";
	public string Model { get; set; } = "gpt-5.4-mini";
	public string OrganizationId { get; set; } = "";
	public int MaxDiffChars { get; set; } = 8000;
}
```

- [ ] **Step 4: Add Llm to PersistentState**

Modify `CrossRepoActions/PersistentState.cs` to add the using and property:
```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CrossRepoActions;

using System.Collections.ObjectModel;

using ktsu.AppDataStorage;
using ktsu.CrossRepoActions.Llm;
using ktsu.Semantics.Paths;

internal sealed class PersistentState : AppData<PersistentState>
{
	public Collection<AbsoluteDirectoryPath> CachedRepos { get; set; } = [];
	public Collection<Solution> CachedSolutions { get; set; } = [];
	public LlmSettings Llm { get; set; } = new();
}
```

- [ ] **Step 5: Build to verify packages restore and compile**

Run:
```bash
cd /c/dev/ktsu-dev/CrossRepoActions && dotnet build
```
Expected: build succeeds. If NuGet audit reports transitive vulnerability warnings (treated as errors), pin the offending transitive package in `Directory.Packages.props` with a matching `<PackageVersion>` and re-run; note any such pin in the commit message.

- [ ] **Step 6: Commit**

```bash
git add Directory.Packages.props CrossRepoActions/CrossRepoActions.csproj CrossRepoActions/Llm/LlmSettings.cs CrossRepoActions/PersistentState.cs
git commit -m "feat: add Semantic Kernel packages and LLM settings"
```

---

### Task 3: ILlmService + CommitMessageGenerator (TDD)

The pure, testable core: prompt construction + response cleaning.

**Files:**
- Create: `CrossRepoActions/Llm/ILlmService.cs`
- Create: `CrossRepoActions/Llm/CommitMessageGenerator.cs`
- Test: `CrossRepoActions.Test/CommitMessageGeneratorTests.cs`

**Interfaces:**
- Produces:
  - `interface ILlmService { Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default); }`
  - `class CommitMessageGenerator(ILlmService llmService)` with:
    - `Task<string> GenerateAsync(string diffStat, string diff, bool truncated, CancellationToken cancellationToken = default)`
    - `static string BuildSystemPrompt()`
    - `static string BuildUserPrompt(string diffStat, string diff, bool truncated)`
    - `static string CleanResponse(string raw)`

- [ ] **Step 1: Create the interface**

`CrossRepoActions/Llm/ILlmService.cs`:
```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CrossRepoActions.Llm;

internal interface ILlmService
{
	Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Write the failing tests**

`CrossRepoActions.Test/CommitMessageGeneratorTests.cs`:
```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CrossRepoActions.Test;

using System;
using System.Threading;
using System.Threading.Tasks;

using ktsu.CrossRepoActions.Llm;

using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class CommitMessageGeneratorTests
{
	private sealed class FakeLlmService : ILlmService
	{
		public string? CapturedSystem { get; private set; }
		public string? CapturedUser { get; private set; }
		public string Response { get; set; } = "";

		public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
		{
			CapturedSystem = systemPrompt;
			CapturedUser = userPrompt;
			return Task.FromResult(Response);
		}
	}

	[TestMethod]
	public void BuildUserPrompt_IncludesStatAndDiff()
	{
		string prompt = CommitMessageGenerator.BuildUserPrompt("1 file changed", "diff --git a b", truncated: false);
		StringAssert.Contains(prompt, "1 file changed");
		StringAssert.Contains(prompt, "diff --git a b");
	}

	[TestMethod]
	public void BuildUserPrompt_NotesTruncation_WhenTruncated()
	{
		string prompt = CommitMessageGenerator.BuildUserPrompt("stat", "diff", truncated: true);
		StringAssert.Contains(prompt, "truncated");
	}

	[TestMethod]
	public void BuildUserPrompt_OmitsTruncationNote_WhenNotTruncated()
	{
		string prompt = CommitMessageGenerator.BuildUserPrompt("stat", "diff", truncated: false);
		Assert.AreEqual(-1, prompt.IndexOf("truncated", StringComparison.OrdinalIgnoreCase));
	}

	[TestMethod]
	public void CleanResponse_StripsCodeFences()
	{
		string cleaned = CommitMessageGenerator.CleanResponse("```\nfeat: add thing\n```");
		Assert.AreEqual("feat: add thing", cleaned);
	}

	[TestMethod]
	public void CleanResponse_StripsCodeFencesWithLanguageHint()
	{
		string cleaned = CommitMessageGenerator.CleanResponse("```text\nfix: bug\n```");
		Assert.AreEqual("fix: bug", cleaned);
	}

	[TestMethod]
	public void CleanResponse_StripsWrappingQuotes()
	{
		string cleaned = CommitMessageGenerator.CleanResponse("\"fix: correct bug\"");
		Assert.AreEqual("fix: correct bug", cleaned);
	}

	[TestMethod]
	public void CleanResponse_TrimsWhitespace()
	{
		string cleaned = CommitMessageGenerator.CleanResponse("  chore: tidy up \n");
		Assert.AreEqual("chore: tidy up", cleaned);
	}

	[TestMethod]
	public async Task GenerateAsync_ReturnsCleanedResponse_AndSendsBothPrompts()
	{
		FakeLlmService fake = new() { Response = "```\nfeat: new feature\n```" };
		CommitMessageGenerator generator = new(fake);

		string message = await generator.GenerateAsync("stat", "diff body", truncated: false);

		Assert.AreEqual("feat: new feature", message);
		Assert.IsNotNull(fake.CapturedSystem);
		StringAssert.Contains(fake.CapturedUser, "diff body");
	}
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run:
```bash
cd /c/dev/ktsu-dev/CrossRepoActions && dotnet test --filter "FullyQualifiedName~CommitMessageGeneratorTests"
```
Expected: compile error / FAIL — `CommitMessageGenerator` does not exist yet.

- [ ] **Step 4: Implement CommitMessageGenerator**

`CrossRepoActions/Llm/CommitMessageGenerator.cs`:
```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CrossRepoActions.Llm;

using System.Text;

internal sealed class CommitMessageGenerator(ILlmService llmService)
{
	internal async Task<string> GenerateAsync(string diffStat, string diff, bool truncated, CancellationToken cancellationToken = default)
	{
		string raw = await llmService
			.CompleteAsync(BuildSystemPrompt(), BuildUserPrompt(diffStat, diff, truncated), cancellationToken)
			.ConfigureAwait(false);
		return CleanResponse(raw);
	}

	internal static string BuildSystemPrompt() =>
		"You are an expert software engineer writing a git commit message. " +
		"Analyse the supplied diff and produce a single commit message in Conventional Commits style. " +
		"The first line must be a concise summary of the form '<type>: <description>' " +
		"where type is one of feat, fix, docs, style, refactor, perf, test, build, ci, chore, " +
		"and must be no longer than 72 characters. " +
		"If the change warrants explanation, add a blank line followed by a short body. " +
		"Do NOT include a version tag such as [major], [minor], or [patch]. " +
		"Do NOT wrap the message in markdown code fences or quotation marks. " +
		"Respond with the commit message only.";

	internal static string BuildUserPrompt(string diffStat, string diff, bool truncated)
	{
		StringBuilder sb = new();
		sb.AppendLine("Summary of changes (git diff --stat):");
		sb.AppendLine(diffStat);
		sb.AppendLine();
		if (truncated)
		{
			sb.AppendLine("NOTE: the diff below was truncated because it was large; base your message on what is visible.");
		}

		sb.AppendLine("Diff:");
		sb.AppendLine(diff);
		return sb.ToString();
	}

	internal static string CleanResponse(string raw)
	{
		string text = (raw ?? "").Trim();

		// Strip a wrapping triple-backtick code fence (with optional language hint on the first line).
		if (text.StartsWith("```", StringComparison.Ordinal))
		{
			int firstNewline = text.IndexOf('\n');
			if (firstNewline >= 0)
			{
				text = text[(firstNewline + 1)..];
			}

			if (text.EndsWith("```", StringComparison.Ordinal))
			{
				text = text[..^3];
			}

			text = text.Trim();
		}

		// Strip a single pair of wrapping quotes.
		if (text.Length >= 2
			&& ((text[0] == '"' && text[^1] == '"') || (text[0] == '\'' && text[^1] == '\'')))
		{
			text = text[1..^1].Trim();
		}

		return text;
	}
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run:
```bash
cd /c/dev/ktsu-dev/CrossRepoActions && dotnet test --filter "FullyQualifiedName~CommitMessageGeneratorTests"
```
Expected: all 8 tests PASS.

- [ ] **Step 6: Commit**

```bash
git add CrossRepoActions/Llm/ILlmService.cs CrossRepoActions/Llm/CommitMessageGenerator.cs CrossRepoActions.Test/CommitMessageGeneratorTests.cs
git commit -m "feat: add commit message generator and LLM service interface"
```

---

### Task 4: SemanticKernelLlmService

Wire `ILlmService` to OpenAI via Semantic Kernel. No unit tests (network I/O); verified by build.

**Files:**
- Create: `CrossRepoActions/Llm/SemanticKernelLlmService.cs`

**Interfaces:**
- Consumes: `ILlmService`, `LlmSettings`.
- Produces: `class SemanticKernelLlmService(LlmSettings settings) : ILlmService`.

- [ ] **Step 1: Implement the service**

`CrossRepoActions/Llm/SemanticKernelLlmService.cs`:
```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CrossRepoActions.Llm;

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

internal sealed class SemanticKernelLlmService : ILlmService
{
	private readonly IChatCompletionService chatService;

	internal SemanticKernelLlmService(LlmSettings settings)
	{
		ArgumentNullException.ThrowIfNull(settings);
		if (string.IsNullOrWhiteSpace(settings.ApiKey))
		{
			throw new InvalidOperationException("No OpenAI API key configured. Run the ConfigureLlm verb to set one.");
		}

		IKernelBuilder builder = Kernel.CreateBuilder();
		string? orgId = string.IsNullOrWhiteSpace(settings.OrganizationId) ? null : settings.OrganizationId;
		_ = builder.AddOpenAIChatCompletion(settings.Model, settings.ApiKey, orgId);
		Kernel kernel = builder.Build();
		chatService = kernel.GetRequiredService<IChatCompletionService>();
	}

	public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
	{
		ChatHistory history = new();
		history.AddSystemMessage(systemPrompt);
		history.AddUserMessage(userPrompt);

		ChatMessageContent result = await chatService
			.GetChatMessageContentAsync(history, kernel: null, cancellationToken: cancellationToken)
			.ConfigureAwait(false);

		return result.Content ?? "";
	}
}
```

- [ ] **Step 2: Build to verify the SK API usage compiles**

Run:
```bash
cd /c/dev/ktsu-dev/CrossRepoActions && dotnet build
```
Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add CrossRepoActions/Llm/SemanticKernelLlmService.cs
git commit -m "feat: add Semantic Kernel OpenAI chat completion service"
```

---

### Task 5: Git additions

Add diff/stage/diff-tool helpers to `Git.cs`. PowerShell wrappers, no unit tests (match existing approach); verified by build.

**Files:**
- Modify: `CrossRepoActions/Git.cs`

**Interfaces:**
- Produces (all `internal static`, in class `Git`):
  - `(string Diff, bool Truncated) GetDiff(AbsoluteDirectoryPath repo, int maxChars)`
  - `string GetDiffStat(AbsoluteDirectoryPath repo)`
  - `IEnumerable<string> StageAll(AbsoluteDirectoryPath repo)`
  - `IEnumerable<string> OpenDiffTool(AbsoluteDirectoryPath repo)`

- [ ] **Step 1: Add the methods**

Append these methods inside the `Git` class in `CrossRepoActions/Git.cs` (before the closing brace, after `GetUpstreamAheadBehind`):
```csharp
	private static IReadOnlyList<string> GetUntrackedFiles(AbsoluteDirectoryPath repo)
	{
		using PowerShell ps = PowerShell.Create();
		return [.. ps
			.AddCommand("git")
			.AddArgument("-C")
			.AddArgument(repo.ToString())
			.AddArgument("ls-files")
			.AddArgument("--others")
			.AddArgument("--exclude-standard")
			.InvokeAndReturnOutput(PowershellStreams.All)
			.Where(s => !string.IsNullOrWhiteSpace(s))];
	}

	/// <summary>
	/// Gets the working-tree diff against HEAD (staged + unstaged changes to tracked files),
	/// plus a list of untracked file paths (which <c>git diff HEAD</c> omits). The result is
	/// truncated to <paramref name="maxChars"/>; <c>Truncated</c> reports whether it was cut.
	/// </summary>
	internal static (string Diff, bool Truncated) GetDiff(AbsoluteDirectoryPath repo, int maxChars)
	{
		using PowerShell ps = PowerShell.Create();
		Collection<string> results = ps
			.AddCommand("git")
			.AddArgument("-C")
			.AddArgument(repo.ToString())
			.AddArgument("diff")
			.AddArgument("HEAD")
			.InvokeAndReturnOutput(PowershellStreams.All);

		string diff = string.Join(Environment.NewLine, results);

		IReadOnlyList<string> untracked = GetUntrackedFiles(repo);
		if (untracked.Count > 0)
		{
			diff += $"{Environment.NewLine}{Environment.NewLine}# Untracked files:{Environment.NewLine}"
				+ string.Join(Environment.NewLine, untracked);
		}

		bool truncated = diff.Length > maxChars;
		if (truncated)
		{
			diff = diff[..maxChars];
		}

		return (diff, truncated);
	}

	/// <summary>
	/// Gets the <c>git diff HEAD --stat</c> summary, with untracked file names appended so the
	/// summary reflects brand-new files too.
	/// </summary>
	internal static string GetDiffStat(AbsoluteDirectoryPath repo)
	{
		using PowerShell ps = PowerShell.Create();
		Collection<string> results = ps
			.AddCommand("git")
			.AddArgument("-C")
			.AddArgument(repo.ToString())
			.AddArgument("diff")
			.AddArgument("HEAD")
			.AddArgument("--stat")
			.InvokeAndReturnOutput(PowershellStreams.All);

		string stat = string.Join(Environment.NewLine, results);

		IReadOnlyList<string> untracked = GetUntrackedFiles(repo);
		if (untracked.Count > 0)
		{
			stat += $"{Environment.NewLine}Untracked: {string.Join(", ", untracked)}";
		}

		return stat;
	}

	internal static IEnumerable<string> StageAll(AbsoluteDirectoryPath repo)
	{
		using PowerShell ps = PowerShell.Create();
		return ps
			.AddCommand("git")
			.AddArgument("-C")
			.AddArgument(repo.ToString())
			.AddArgument("add")
			.AddArgument("-A")
			.InvokeAndReturnOutput(PowershellStreams.All);
	}

	/// <summary>
	/// Opens the configured git diff tool for the working tree. If no diff tool is configured,
	/// the returned output contains git's error text and the caller falls back to printing the diff.
	/// </summary>
	internal static IEnumerable<string> OpenDiffTool(AbsoluteDirectoryPath repo)
	{
		using PowerShell ps = PowerShell.Create();
		return ps
			.AddCommand("git")
			.AddArgument("-C")
			.AddArgument(repo.ToString())
			.AddArgument("difftool")
			.AddArgument("-d")
			.AddArgument("--no-prompt")
			.InvokeAndReturnOutput(PowershellStreams.All);
	}
```

- [ ] **Step 2: Build to verify**

Run:
```bash
cd /c/dev/ktsu-dev/CrossRepoActions && dotnet build
```
Expected: build succeeds. (`Collection<>` and `Linq` are already imported in `Git.cs`.)

- [ ] **Step 3: Commit**

```bash
git add CrossRepoActions/Git.cs
git commit -m "feat: add diff, stage-all, and difftool git helpers"
```

---

### Task 6: ConfigureLlm verb

Interactive verb to set OpenAI settings; masks the API key.

**Files:**
- Create: `CrossRepoActions/Verbs/ConfigureLlm.cs`

**Interfaces:**
- Consumes: `PersistentState.Get().Llm` (`LlmSettings`), `BaseVerb<T>`.
- Produces: a `[Verb("ConfigureLlm")]` that persists `LlmSettings` via `state.Save()`.

- [ ] **Step 1: Implement the verb**

`CrossRepoActions/Verbs/ConfigureLlm.cs`:
```csharp
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
```

- [ ] **Step 2: Build to verify**

Run:
```bash
cd /c/dev/ktsu-dev/CrossRepoActions && dotnet build
```
Expected: build succeeds.

- [ ] **Step 3: Manual smoke check (optional but recommended)**

Run:
```bash
cd /c/dev/ktsu-dev/CrossRepoActions && dotnet run --project CrossRepoActions/CrossRepoActions.csproj -- ConfigureLlm
```
Expected: prompts for Model / Org / Max diff chars / API key (masked input); prints `Saved.`

- [ ] **Step 4: Commit**

```bash
git add CrossRepoActions/Verbs/ConfigureLlm.cs
git commit -m "feat: add ConfigureLlm verb for OpenAI settings"
```

---

### Task 7: SuggestCommits verb

The orchestrator: fetch → behind-check/pull → generate → review (commit/push/edit/regenerate/diff/skip) with per-action remote re-check and per-phase progress. Also extracts the shared ahead/behind formatter (now used by two verbs).

**Files:**
- Create: `CrossRepoActions/Verbs/Formatting.cs`
- Modify: `CrossRepoActions/Verbs/ListRepositories.cs` (use shared formatter)
- Create: `CrossRepoActions/Verbs/SuggestCommits.cs`

**Interfaces:**
- Consumes: `Git.DiscoverRepositories`, `Git.GetStatusSummary`, `Git.Fetch`, `Git.Pull`, `Git.Push`, `Git.Commit`, `Git.StageAll`, `Git.GetDiff`, `Git.GetDiffStat`, `Git.OpenDiffTool`, `Git.GetCurrentBranch`, `Git.GetUpstreamBranch`, `Git.GetUpstreamAheadBehind`; `CommitMessageGenerator`, `SemanticKernelLlmService`, `LlmSettings`, `Program.MaxParallelism`.
- Produces: `[Verb("SuggestCommits")]`; `static string Formatting.FormatAheadBehind((int Ahead, int Behind)? aheadBehind)`.

- [ ] **Step 1: Extract the shared formatter**

`CrossRepoActions/Verbs/Formatting.cs`:
```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CrossRepoActions.Verbs;

internal static class Formatting
{
	internal static string FormatAheadBehind((int Ahead, int Behind)? aheadBehind)
	{
		if (aheadBehind is null)
		{
			return "—";
		}

		(int ahead, int behind) = aheadBehind.Value;
		if (ahead == 0 && behind == 0)
		{
			return "≡";
		}

		List<string> parts = [];
		if (ahead > 0)
		{
			parts.Add($"↑{ahead}");
		}

		if (behind > 0)
		{
			parts.Add($"↓{behind}");
		}

		return string.Join(" ", parts);
	}
}
```

- [ ] **Step 2: Update ListRepositories to use it**

In `CrossRepoActions/Verbs/ListRepositories.cs`, replace the two `FormatAheadBehind(...)` call sites (lines ~73 and ~76) with `Formatting.FormatAheadBehind(...)`, and delete the private `FormatAheadBehind` method (lines ~84-109). The two call sites become:
```csharp
			string upstreamStr = Formatting.FormatAheadBehind(status.Upstream);

			string vsDefaultStr = status.VsDefault is not null
				? $"  {status.DefaultRef}: {Formatting.FormatAheadBehind(status.VsDefault)}"
				: "";
```

- [ ] **Step 3: Build to verify the refactor is behaviour-preserving**

Run:
```bash
cd /c/dev/ktsu-dev/CrossRepoActions && dotnet build
```
Expected: build succeeds (no remaining reference to the deleted private method).

- [ ] **Step 4: Implement SuggestCommits**

`CrossRepoActions/Verbs/SuggestCommits.cs`:
```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CrossRepoActions.Verbs;

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

using CommandLine;

using ktsu.CrossRepoActions.Llm;
using ktsu.Semantics.Paths;

[Verb("SuggestCommits")]
internal sealed class SuggestCommits : BaseVerb<SuggestCommits>
{
	private static readonly object ConsoleLock = new();

	private sealed record Suggestion(
		AbsoluteDirectoryPath Repo,
		string Name,
		string DiffStat,
		string Message,
		bool Truncated,
		string? Error);

	internal override void Run(SuggestCommits options)
	{
		LlmSettings settings = PersistentState.Get().Llm;
		if (string.IsNullOrWhiteSpace(settings.ApiKey))
		{
			Console.WriteLine("No OpenAI API key configured. Run the ConfigureLlm verb first.");
			return;
		}

		CommitMessageGenerator generator = new(new SemanticKernelLlmService(settings));

		List<AbsoluteDirectoryPath> dirty = [.. Git.DiscoverRepositories(options.Path)
			.Where(r => Git.GetStatusSummary(r) != "clean")];

		if (dirty.Count == 0)
		{
			Console.WriteLine("No repositories with outstanding local changes.");
			return;
		}

		FetchAll(dirty);
		List<AbsoluteDirectoryPath> toProcess = PromptPullWhereBehind(dirty);
		List<Suggestion> suggestions = GenerateAll(toProcess, generator, settings);
		Review(suggestions, generator, settings);
	}

	private static void FetchAll(List<AbsoluteDirectoryPath> repos)
	{
		int done = 0;
		int total = repos.Count;
		_ = Parallel.ForEach(repos, new() { MaxDegreeOfParallelism = Program.MaxParallelism }, repo =>
		{
			_ = Git.Fetch(repo).ToList();
			int n = Interlocked.Increment(ref done);
			lock (ConsoleLock)
			{
				Console.Write($"\rFetching {n}/{total}…   ");
			}
		});

		Console.WriteLine($"\rFetching done ({total}).        ");
	}

	private static List<AbsoluteDirectoryPath> PromptPullWhereBehind(List<AbsoluteDirectoryPath> repos)
	{
		List<AbsoluteDirectoryPath> result = [];
		int total = repos.Count;
		for (int i = 0; i < total; i++)
		{
			AbsoluteDirectoryPath repo = repos[i];
			string name = System.IO.Path.GetFileName(repo.ToString());
			(int Ahead, int Behind)? ab = Git.GetUpstreamAheadBehind(repo);
			if (ab is { Behind: > 0 })
			{
				string? upstream = Git.GetUpstreamBranch(repo);
				Console.Write($"[{i + 1}/{total}] {name} is behind {upstream} by {ab.Value.Behind}. Pull before generating? [Y/n] ");
				string ans = (Console.ReadLine() ?? "").Trim().ToUpperInvariant();
				if (ans is not ("N" or "NO"))
				{
					List<string> pullOutput = [.. Git.Pull(repo)];
					if (HasConflict(pullOutput))
					{
						Console.WriteLine($"  pull conflicted in {name} — skipping this repo.");
						continue;
					}
				}
			}

			result.Add(repo);
		}

		return result;
	}

	private static List<Suggestion> GenerateAll(List<AbsoluteDirectoryPath> repos, CommitMessageGenerator generator, LlmSettings settings)
	{
		int done = 0;
		int total = repos.Count;
		ConcurrentBag<Suggestion> bag = [];
		_ = Parallel.ForEach(repos, new() { MaxDegreeOfParallelism = Program.MaxParallelism }, repo =>
		{
			bag.Add(GenerateOne(repo, generator, settings));
			int n = Interlocked.Increment(ref done);
			lock (ConsoleLock)
			{
				Console.Write($"\rGenerating {n}/{total}…   ");
			}
		});

		Console.WriteLine($"\rGenerating done ({total}).        ");
		return [.. bag];
	}

	[SuppressMessage("Design", "CA1031:Do not catch general exception types",
		Justification = "A per-repo generation failure (network, auth, parsing) must not abort the whole batch; the error is surfaced to the user in the review phase.")]
	private static Suggestion GenerateOne(AbsoluteDirectoryPath repo, CommitMessageGenerator generator, LlmSettings settings)
	{
		string name = System.IO.Path.GetFileName(repo.ToString());
		try
		{
			string stat = Git.GetDiffStat(repo);
			(string diff, bool truncated) = Git.GetDiff(repo, settings.MaxDiffChars);
			string message = generator.GenerateAsync(stat, diff, truncated).GetAwaiter().GetResult();
			return new(repo, name, stat, message, truncated, null);
		}
		catch (Exception ex)
		{
			return new(repo, name, "", "", false, ex.Message);
		}
	}

	private static void Review(List<Suggestion> suggestions, CommitMessageGenerator generator, LlmSettings settings)
	{
		List<Suggestion> ordered = [.. suggestions.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)];
		int total = ordered.Count;
		for (int i = 0; i < total; i++)
		{
			ReviewOne(ordered[i], i + 1, total, generator, settings);
		}
	}

	private static void ReviewOne(Suggestion suggestion, int index, int total, CommitMessageGenerator generator, LlmSettings settings)
	{
		Console.WriteLine();
		Console.WriteLine($"[{index}/{total}] {suggestion.Name}");

		if (suggestion.Error is not null)
		{
			Console.WriteLine($"  generation failed: {suggestion.Error}");
			return;
		}

		string branch = Git.GetCurrentBranch(suggestion.Repo);
		string? upstream = Git.GetUpstreamBranch(suggestion.Repo);
		(int Ahead, int Behind)? ab = Git.GetUpstreamAheadBehind(suggestion.Repo);
		string branchLine = upstream is null
			? $"  branch: {branch} (no upstream)"
			: $"  branch: {branch} → {upstream} {Formatting.FormatAheadBehind(ab)}";
		Console.WriteLine(branchLine);

		Console.WriteLine("  changes:");
		foreach (string line in suggestion.DiffStat.Split('\n'))
		{
			Console.WriteLine($"    {line.TrimEnd()}");
		}

		if (suggestion.Truncated)
		{
			Console.WriteLine("  ⚠ diff was truncated before sending to the model — use [D]iff to see the full changes.");
		}

		string message = suggestion.Message;
		bool resolved = false;
		while (!resolved)
		{
			Console.WriteLine();
			Console.WriteLine("  suggested message:");
			Console.WriteLine($"    {message.Replace("\n", "\n    ", StringComparison.Ordinal)}");
			Console.Write("  [C]ommit / [P]ush / [E]dit / [R]egenerate / [D]iff / [S]kip ? ");
			string choice = (Console.ReadLine() ?? "").Trim().ToUpperInvariant();
			switch (choice)
			{
				case "C":
					resolved = CommitRepo(suggestion.Repo, message, push: false);
					break;
				case "P":
					resolved = CommitRepo(suggestion.Repo, message, push: true);
					break;
				case "E":
					message = EditMessage(message);
					break;
				case "R":
					message = Regenerate(suggestion, generator, settings) ?? message;
					break;
				case "D":
					ShowDiff(suggestion.Repo);
					break;
				case "S":
					Console.WriteLine("  skipped.");
					resolved = true;
					break;
				default:
					Console.WriteLine("  unrecognised choice.");
					break;
			}
		}
	}

	private static bool CommitRepo(AbsoluteDirectoryPath repo, string message, bool push)
	{
		if (!EnsureRemoteCurrent(repo))
		{
			return false;
		}

		foreach (string line in Git.StageAll(repo))
		{
			Console.WriteLine($"    {line}");
		}

		foreach (string line in Git.Commit(repo, message))
		{
			Console.WriteLine($"    {line}");
		}

		if (push)
		{
			foreach (string line in Git.Push(repo))
			{
				Console.WriteLine($"    {line}");
			}
		}

		Console.WriteLine(push ? "  committed and pushed." : "  committed.");
		return true;
	}

	private static bool EnsureRemoteCurrent(AbsoluteDirectoryPath repo)
	{
		Console.WriteLine("  fetching latest from remote…");
		_ = Git.Fetch(repo).ToList();

		(int Ahead, int Behind)? ab = Git.GetUpstreamAheadBehind(repo);
		if (ab is { Behind: > 0 })
		{
			Console.Write($"  remote has {ab.Value.Behind} new commit(s). Pull (--autostash) before continuing? [Y/n] ");
			string ans = (Console.ReadLine() ?? "").Trim().ToUpperInvariant();
			if (ans is "N" or "NO")
			{
				Console.WriteLine("  cancelled.");
				return false;
			}

			List<string> pullOutput = [.. Git.Pull(repo)];
			foreach (string line in pullOutput)
			{
				Console.WriteLine($"    {line}");
			}

			if (HasConflict(pullOutput))
			{
				Console.WriteLine("  pull resulted in conflicts — resolve manually. Action aborted.");
				return false;
			}
		}

		return true;
	}

	private static string EditMessage(string current)
	{
		Console.WriteLine("  enter the new commit message; finish with an empty line:");
		List<string> lines = [];
		string? line;
		while (!string.IsNullOrEmpty(line = Console.ReadLine()))
		{
			lines.Add(line);
		}

		string edited = string.Join("\n", lines).Trim();
		return string.IsNullOrWhiteSpace(edited) ? current : edited;
	}

	private static string? Regenerate(Suggestion suggestion, CommitMessageGenerator generator, LlmSettings settings)
	{
		Console.WriteLine("  regenerating…");
		Suggestion fresh = GenerateOne(suggestion.Repo, generator, settings);
		if (fresh.Error is not null)
		{
			Console.WriteLine($"  regeneration failed: {fresh.Error}");
			return null;
		}

		return fresh.Message;
	}

	private static void ShowDiff(AbsoluteDirectoryPath repo)
	{
		List<string> output = [.. Git.OpenDiffTool(repo)];
		bool failed = output.Any(l =>
			l.Contains("fatal", StringComparison.OrdinalIgnoreCase)
			|| l.Contains("not a valid", StringComparison.OrdinalIgnoreCase)
			|| l.Contains("No known", StringComparison.OrdinalIgnoreCase));

		if (output.Count == 0 || failed)
		{
			Console.WriteLine("  no diff tool available — printing diff:");
			(string diff, _) = Git.GetDiff(repo, int.MaxValue);
			Console.WriteLine(diff);
		}
		else
		{
			foreach (string line in output)
			{
				Console.WriteLine($"    {line}");
			}
		}
	}

	private static bool HasConflict(IEnumerable<string> gitOutput) =>
		gitOutput.Any(l =>
			l.Contains("CONFLICT", StringComparison.OrdinalIgnoreCase)
			|| l.Contains("Automatic merge failed", StringComparison.OrdinalIgnoreCase));
}
```

- [ ] **Step 5: Build to verify**

Run:
```bash
cd /c/dev/ktsu-dev/CrossRepoActions && dotnet build
```
Expected: build succeeds with no warnings (warnings are errors).

- [ ] **Step 6: Run the full test suite**

Run:
```bash
cd /c/dev/ktsu-dev/CrossRepoActions && dotnet test
```
Expected: all tests PASS (smoke + 8 generator tests).

- [ ] **Step 7: Manual end-to-end check (recommended, requires an API key + a dirty repo)**

Run:
```bash
cd /c/dev/ktsu-dev/CrossRepoActions && dotnet run --project CrossRepoActions/CrossRepoActions.csproj -- SuggestCommits -p c:/dev/ktsu-dev
```
Expected: `Fetching N/total…` then `Generating N/total…` progress; for each dirty repo a `[i/total]` review block showing branch + diff stat + suggested message and the `[C]/[P]/[E]/[R]/[D]/[S]` prompt. Verify `S` skips without changes; `D` opens the diff tool (or prints the diff); `C`/`P` re-fetch then commit/push.

- [ ] **Step 8: Commit**

```bash
git add CrossRepoActions/Verbs/Formatting.cs CrossRepoActions/Verbs/ListRepositories.cs CrossRepoActions/Verbs/SuggestCommits.cs
git commit -m "feat: add SuggestCommits verb for AI commit messages"
```

---

## Post-implementation

- [ ] Run the `update-docs` skill to refresh `README.md` / `CLAUDE.md` for the new verbs and SK dependency (the repo CLAUDE.md currently states .NET 9 / ktsu.Sdk.App 1.8.0, which is already stale — flag/update as part of this).
- [ ] Final `dotnet build && dotnet test` clean run before opening a PR.
