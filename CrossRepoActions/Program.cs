// Ignore Spelling: sha

[assembly: CLSCompliant(false)]
[assembly: System.Runtime.InteropServices.ComVisible(false)]

namespace ktsu.CrossRepoActions;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using CommandLine;
using ktsu.CrossRepoActions.Verbs;

internal static class Program
{
	internal static PersistentState Settings { get; set; } = new();

	[RequiresUnreferencedCode("Calls ktsu.CrossRepoActions.Program.LoadVerbs()")]
	private static void Main(string[] args)
	{
		Console.OutputEncoding = Encoding.UTF8;
		Settings = PersistentState.LoadOrCreate();

		var types = LoadVerbs();

		_ = Parser.Default.ParseArguments(args, types)
			.WithParsed<BaseVerb>(task => task.Run());
	}

	[RequiresUnreferencedCode("Calls System.Reflection.Assembly.GetTypes()")]
	private static Type[] LoadVerbs()
	{
		return Assembly.GetExecutingAssembly().GetTypes()
			.Where(t => t.GetCustomAttribute<VerbAttribute>() != null).ToArray();
	}
}
