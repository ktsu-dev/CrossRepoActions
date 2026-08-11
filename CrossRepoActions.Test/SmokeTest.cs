// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.CrossRepoActions.Test;

using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class SmokeTest
{
	[TestMethod]
	[SuppressMessage("Usage", "MSTEST0032", Justification = "Program.MaxParallelism is a compile-time constant so the equality is statically known; the test's real purpose is to verify InternalsVisibleTo access compiles.")]
	public void InternalsAreVisibleToTests()
	{
		Assert.AreEqual(-1, Program.MaxParallelism);
	}
}
