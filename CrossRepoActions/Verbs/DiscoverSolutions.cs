// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

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
