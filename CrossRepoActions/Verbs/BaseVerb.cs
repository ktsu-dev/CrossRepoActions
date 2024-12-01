namespace ktsu.CrossRepoActions.Verbs;

using CommandLine;
using ktsu.Extensions;
using ktsu.StrongPaths;

internal abstract class BaseVerb
{
	[Option('p', "path", Required = false, HelpText = "The root path to discover solutions from.")]
	public string PathString { get; set; } = "c:/dev/ktsu-dev";
	internal AbsoluteDirectoryPath Path => PathString.As<AbsoluteDirectoryPath>();

	internal abstract void Run();

	internal virtual bool ValidateArgs() => true;
}

internal abstract class BaseVerb<T> : BaseVerb where T : BaseVerb<T>
{
	internal override void Run()
	{
		if (!ValidateArgs())
		{
			return;
		}

		Run((T)this);
	}

	internal abstract void Run(T options);
}
