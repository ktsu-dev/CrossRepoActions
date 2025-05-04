// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CrossRepoActions;

using System.Collections.ObjectModel;

using ktsu.AppDataStorage;
using ktsu.StrongPaths;

internal class PersistentState : AppData<PersistentState>
{
	public Collection<AbsoluteDirectoryPath> CachedRepos { get; set; } = [];
	public Collection<Solution> CachedSolutions { get; set; } = [];
}
