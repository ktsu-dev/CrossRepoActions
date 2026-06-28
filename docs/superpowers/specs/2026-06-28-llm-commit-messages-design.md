# Design: Semantic Kernel LLM integration & AI commit-message suggestions

**Date:** 2026-06-28
**Status:** Approved (pending spec review)

## Overview

Add Microsoft Semantic Kernel to CrossRepoActions to enable interaction with remote
LLMs, behind a small `ILlmService` abstraction. Ship a first feature — the
`SuggestCommits` verb — that generates suggested commit messages for repositories with
uncommitted local changes, then lets the user review and commit/push them.

Commit messages use plain **Conventional Commits** style (`feat:`, `fix:`, `chore:`, …).
No PSBuild version tag is emitted: PSBuild infers the version bump automatically when no
`[major]`/`[minor]`/`[patch]`/`[pre]` tag is present.

The provider for this first iteration is **OpenAI** (api.openai.com). Configuration is
stored in the existing persistent app state.

## Goals

- Introduce a reusable, testable LLM abstraction backed by Semantic Kernel.
- Generate quality commit-message suggestions from a repo's working-tree diff.
- Keep the human in control: review every suggestion, choose commit / push / edit /
  regenerate / skip per repo.
- Be safe around remotes: fetch before generating, prompt to pull when behind, and
  re-check the remote immediately before committing/pushing.

## Non-Goals

- Multiple LLM providers (Azure, Anthropic, Ollama). The abstraction leaves room, but
  only OpenAI is wired up now.
- Encrypted secret storage. The API key is stored in plaintext app data (see Security).
- Generating messages for already-staged-and-clean repos or for individual hunks.

## Configuration (persistent app state)

Extend `PersistentState` with an `LlmSettings` object:

| Field            | Type     | Default        | Notes                                        |
|------------------|----------|----------------|----------------------------------------------|
| `ApiKey`         | `string` | `""`           | OpenAI API key. Masked on display.           |
| `Model`          | `string` | `gpt-5.4-mini` | OpenAI chat model id.                         |
| `OrganizationId` | `string` | `""`           | Optional OpenAI org id.                       |
| `MaxDiffChars`   | `int`    | `8000`         | Per-repo cap on diff text sent to the model.  |

New verb **`ConfigureLlm`** sets these interactively:
- Prompts for model (showing current), API key (input masked), optional org id, and
  max diff chars.
- When displaying existing config, the API key is masked to a form like `sk-…wxyz`.
- Persists via the existing `AppDataStorage` mechanism (`PersistentState.Save()`).

`SuggestCommits` fails fast with a clear, actionable message ("run ConfigureLlm to set
your OpenAI API key") when `ApiKey` is empty.

## LLM layer (`Llm/` folder)

### `ILlmService`
```csharp
internal interface ILlmService
{
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
}
```

### `SemanticKernelLlmService : ILlmService`
- Constructed from `LlmSettings`.
- Builds a `Kernel` via `Kernel.CreateBuilder().AddOpenAIChatCompletion(model, apiKey, orgId)`.
- Calls the chat completion service with the system + user prompts and returns the text.
- Throws an `InvalidOperationException` with a clear message when configuration is
  missing/invalid.

### `CommitMessageGenerator`
- Depends only on `ILlmService` plus the supplied git data (diff stat + capped diff
  string + truncation flag). It does **not** call git or PowerShell itself — that makes
  it pure and unit-testable with a fake `ILlmService`.
- Builds the prompt:
  - **System prompt:** instructs the model to act as a commit-message author, output a
    single Conventional Commits message (a `type: summary` subject line ≤ ~72 chars,
    optional blank line + body), no version tag, no markdown fences, no surrounding
    quotes.
  - **User prompt:** includes the `--stat` summary (always full) and the capped diff,
    with an explicit note when the diff was truncated.
- Post-processes the response: trims whitespace, strips wrapping code fences/quotes if
  present, and returns the cleaned message.

## Git additions (`Git.cs`)

New methods (PowerShell-wrapped, consistent with existing style):

- `GetDiff(repo, maxChars)` → `(string Diff, bool Truncated)`.
  Runs `git diff HEAD` (captures staged + unstaged changes to tracked files relative to
  HEAD). Because `git diff HEAD` omits untracked files, also appends the list of
  untracked file paths (`git ls-files --others --exclude-standard`) under an
  `# Untracked files:` heading so brand-new files are visible to the model. Truncates
  the joined output to `maxChars`, setting `Truncated = true` when cut.
- `GetDiffStat(repo)` → `string`. Runs `git diff HEAD --stat`; appends the untracked
  file list (same as above) so the stat reflects new files too. Returned in full.
- `StageAll(repo)` → runs `git add -A`.
- `OpenDiffTool(repo)` → runs `git difftool -d --no-prompt`; if no difftool is
  configured (or it errors), falls back to printing `git diff HEAD` to the console.

Existing methods reused: `Fetch`, `Pull`, `Push`, `Commit`, `GetCurrentBranch`,
`GetUpstreamBranch`, `GetUpstreamAheadBehind`, `GetStatusSummary`, `DiscoverRepositories`.

## New verb: `SuggestCommits`

Auto-discovered via the existing `[Verb]` reflection, so it appears in the interactive
menu automatically. Inherits `BaseVerb<SuggestCommits>` and the shared `-p/--path`
option.

### Lifecycle

1. **Discover & filter** — `Git.DiscoverRepositories(Path)`, keep only repos whose
   `GetStatusSummary` is not `clean`.
2. **Fetch phase (parallel)** — `git fetch --all` on every dirty repo before any
   generation, so ahead/behind is accurate. Uses `Parallel.ForEach` with
   `Program.MaxParallelism`. Shows a **live progress counter** (`Fetching N/total…`)
   that increments as each repo completes (see Progress reporting).
3. **Behind-check / pull prompt (sequential)** — for each dirty repo, compute
   `GetUpstreamAheadBehind`. Prefix each repo with its serial position
   (`[i/total] <repo>`). If the repo is **behind** its upstream, prompt:
   *"<repo> is behind <upstream> by N. Pull before generating? [Y/n]"*.
   - Yes → `git pull --autostash`; on conflict, report and skip that repo.
   - No → proceed using the current local state.
   - Repos with no upstream or in sync skip this step.
4. **Generate phase (parallel)** — for each remaining dirty repo: gather
   `GetDiffStat` + `GetDiff(MaxDiffChars)`, call `CommitMessageGenerator`. Collect
   results and per-repo errors (a generation failure for one repo does not abort the
   batch). Shows a **live progress counter** (`Generating N/total…`) that increments as
   each repo's suggestion completes.
5. **Review phase (sequential)** — for each repo, prefixed with its serial position
   (`[i/total]`), print:
   - repo name
   - **target branch**: the current branch the commit would land on
     (`GetCurrentBranch`), its upstream (`GetUpstreamBranch`), and ahead/behind
   - diff stat
   - the suggested message
   - a **truncation flag** when the diff was capped (with a hint to use `[D]iff`)

   Prompt: `[C]ommit / [P]ush / [E]dit / [R]egenerate / [D]iff / [S]kip`:
   - **Commit** → run the *remote re-check* (below); then `StageAll` + `Commit(message)`.
   - **Push** → run the *remote re-check*; then `StageAll` + `Commit(message)` + `Push`.
     If the push is rejected (e.g. a race), report the error rather than failing silently.
   - **Edit** → user types a replacement message; return to the prompt so they can then
     Commit/Push the edited message.
   - **Regenerate** → re-run generation for this repo, then re-display.
   - **Diff** → `OpenDiffTool(repo)`, then re-display the prompt.
   - **Skip** → leave the repo untouched.

### Progress reporting

Every phase reports progress so long-running batches are legible:

- **Parallel phases (fetch, generate)** — a single live-updating counter line,
  `<verb> N/total…`, where `N` is incremented with `Interlocked.Increment` as each
  repo's task finishes (order-independent). The line is rewritten in place (carriage
  return / `Console` cursor) rather than printing one line per repo, and a final
  `<verb> done (total)` line is emitted when the phase completes.
- **Serial phases (behind-check, review)** — each repo is prefixed with its position
  `[i/total]` as the loop advances from repo to repo, so the user always sees how far
  through the batch they are.

Console writes from parallel phases are synchronised with a lock, matching the existing
pattern used elsewhere in the codebase.

### Remote re-check (before Commit/Push acts)

A shared step invoked at action time, on top of the upfront fetch:

1. Fresh `git fetch` on the repo.
2. Recompute `GetUpstreamAheadBehind`.
3. If the local branch is **behind** (remote diverged / has new commits) → prompt to
   pull first (`git pull --autostash`), then continue; or cancel back to the review
   prompt.
   - If the pull hits conflicts, report them and **abort the action** (nothing is
     committed/pushed) so the user can resolve manually.
4. If in sync or ahead-only → proceed.

This means each acted-on repo sees two fetches over its lifecycle: the upfront
fetch-all before generation, and the just-in-time fetch right before Commit/Push.

## Packages

Add to `Directory.Packages.props` (latest stable 1.x at implementation time):
- `Microsoft.SemanticKernel`
- `Microsoft.SemanticKernel.Connectors.OpenAI`

Referenced by `CrossRepoActions.csproj`.

## Testing

Add a test project **`CrossRepoActions.Test`** following ktsu conventions:
- SDK: `ktsu.Sdk.Test`.
- `TargetFramework` = `net10.0` (latest only; `TargetFrameworks` empty).
- MSTest framework; semantic asserts (no `Assert.IsTrue`/`IsFalse`).
- `ProjectReference` to `CrossRepoActions.csproj`.
- Add `InternalsVisibleTo("ktsu.CrossRepoActions.Test")` to the app's `AssemblyInfo`
  so the internal `CommitMessageGenerator` / `ILlmService` are visible (satisfies the
  ktsu KTSU0002 analyzer).
- Register the project in `CrossRepoActions.sln`.

**Coverage focus** — `CommitMessageGenerator`, the pure logic piece, exercised with a
fake `ILlmService`:
- builds a prompt that includes the diff stat and diff body;
- notes truncation in the prompt when the truncation flag is set;
- strips wrapping code fences / quotes from the model response;
- trims whitespace and returns the cleaned single message.

The thin PowerShell `Git.*` wrappers and `SemanticKernelLlmService` (network I/O) are
not unit-tested, matching the project's existing approach to those layers.

## Security / privacy notes

- The OpenAI API key is stored **in plaintext** in app data (chosen tradeoff;
  consistent with how the app already caches state). It is masked whenever displayed.
- Repo diffs are sent to OpenAI's API for repos the user explicitly runs the verb on.
  This is inherent to the feature; no diff is sent until the generate phase runs.

## Files touched / added

**Added**
- `CrossRepoActions/Llm/ILlmService.cs`
- `CrossRepoActions/Llm/SemanticKernelLlmService.cs`
- `CrossRepoActions/Llm/CommitMessageGenerator.cs`
- `CrossRepoActions/Verbs/ConfigureLlm.cs`
- `CrossRepoActions/Verbs/SuggestCommits.cs`
- `CrossRepoActions.Test/CrossRepoActions.Test.csproj`
- `CrossRepoActions.Test/CommitMessageGeneratorTests.cs`

**Modified**
- `Directory.Packages.props` (SK packages)
- `CrossRepoActions/CrossRepoActions.csproj` (package refs)
- `CrossRepoActions/PersistentState.cs` (`LlmSettings`)
- `CrossRepoActions/Git.cs` (`GetDiff`, `GetDiffStat`, `StageAll`, `OpenDiffTool`)
- `CrossRepoActions/AssemblyInfo` (`InternalsVisibleTo`)
- `CrossRepoActions.sln` (new test project)
