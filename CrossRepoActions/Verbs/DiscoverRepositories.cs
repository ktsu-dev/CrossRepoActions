// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.CrossRepoActions.Verbs;

using CommandLine;

[Verb("DiscoverRepositories")]
internal sealed class DiscoverRepositories : BaseVerb<DiscoverRepositories>
{
	internal override void Run(DiscoverRepositories options)
	{
		PersistentState.Get().CachedRepos.Clear();
		Git.DiscoverRepositories(Path);
	}
}
