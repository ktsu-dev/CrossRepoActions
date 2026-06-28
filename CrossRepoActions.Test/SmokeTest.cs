// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CrossRepoActions.Test;

using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class SmokeTest
{
	[TestMethod]
	public void InternalsAreVisibleToTests()
	{
#pragma warning disable MSTEST0032 // Constant assertion is intentional: verifies InternalsVisibleTo exposes Program.MaxParallelism
		Assert.AreEqual(-1, Program.MaxParallelism);
#pragma warning restore MSTEST0032
	}
}
