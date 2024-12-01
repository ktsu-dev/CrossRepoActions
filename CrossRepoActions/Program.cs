// Ignore Spelling: sha

[assembly: CLSCompliant(false)]
[assembly: System.Runtime.InteropServices.ComVisible(false)]

namespace ktsu.CrossRepoActions;

using System.Reflection;
using System.Text;
using CommandLine;
using ktsu.CrossRepoActions.Verbs;

internal static class Program
{
	internal static Type[] Verbs { get; } = LoadVerbs();
	internal static PersistentState Settings { get; set; } = new();

	private static void Main(string[] args)
	{
		Console.OutputEncoding = Encoding.UTF8;
		Settings = PersistentState.LoadOrCreate();

		_ = Parser.Default.ParseArguments(args, Verbs)
			.WithParsed<BaseVerb>(task => task.Run());
	}

	private static Type[] LoadVerbs()
	{
		return Assembly.GetExecutingAssembly().GetTypes()
			.Where(t => t.GetCustomAttribute<VerbAttribute>() != null).ToArray();
	}
}
