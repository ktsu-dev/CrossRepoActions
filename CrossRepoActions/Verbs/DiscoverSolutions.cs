namespace ktsu.CrossRepoActions.Verbs;

using CommandLine;

[Verb("DiscoverSolutions")]
internal class DiscoverSolutions : BaseVerb<DiscoverSolutions>
{
	internal override void Run(DiscoverSolutions options)
	{
		PersistentState.Get().CachedSolutions.Clear();
		Dotnet.DiscoverSolutions(Path);
	}
}
