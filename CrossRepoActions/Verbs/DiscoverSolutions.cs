// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.CrossRepoActions.Verbs;

using CommandLine;

[Verb("DiscoverSolutions")]
internal sealed class DiscoverSolutions : BaseVerb<DiscoverSolutions>
{
	internal override void Run(DiscoverSolutions options)
	{
		PersistentState.Get().CachedSolutions.Clear();
		Dotnet.DiscoverSolutions(Path);
	}
}
