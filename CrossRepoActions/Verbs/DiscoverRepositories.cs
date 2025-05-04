// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CrossRepoActions.Verbs;

using CommandLine;

[Verb("DiscoverRepositories")]
internal class DiscoverRepositories : BaseVerb<DiscoverRepositories>
{
	internal override void Run(DiscoverRepositories options)
	{
		PersistentState.Get().CachedRepos.Clear();
		Git.DiscoverRepositories(Path);
	}
}
