// Copyright (c) 2023-2026 ktsu-dev contributors

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
