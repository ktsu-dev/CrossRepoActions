// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CrossRepoActions.Test;

using ktsu.CrossRepoActions.Llm;

using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class DiffBudgetTests
{
	private const string Seg1 =
		"diff --git a/Foo.cs b/Foo.cs\n" +
		"index 1111111..2222222 100644\n" +
		"--- a/Foo.cs\n" +
		"+++ b/Foo.cs\n" +
		"@@ -1 +1 @@\n" +
		"-old\n" +
		"+new\n";

	private const string Seg2 =
		"diff --git a/Bar.cs b/Bar.cs\n" +
		"index 3333333..4444444 100644\n" +
		"--- a/Bar.cs\n" +
		"+++ b/Bar.cs\n" +
		"@@ -1 +1 @@\n" +
		"-old2\n" +
		"+new2\n";

	private static readonly string[] bothFiles = ["Foo.cs", "Bar.cs"];
	private static readonly string[] fooOnly = ["Foo.cs"];
	private static readonly string[] barOnly = ["Bar.cs"];
	private static readonly string[] deepPath = ["src/Deep/Path.cs"];

	[TestMethod]
	public void Apply_AllFilesFit_NotTruncated()
	{
		string full = Seg1 + Seg2;
		DiffBudget.Result result = DiffBudget.Apply(full, full.Length);

		Assert.IsFalse(result.Truncated);
		Assert.AreEqual(full, result.PromptDiff);
		Assert.AreEqual(full.Length, result.FullLength);
		CollectionAssert.AreEqual(bothFiles, result.IncludedFiles.ToArray());
		Assert.AreEqual(0, result.OmittedFiles.Count);
	}

	[TestMethod]
	public void Apply_SecondFileWouldClip_OmitsItEntirely()
	{
		string full = Seg1 + Seg2;
		// Cap exactly at the boundary between the two files: file 1 fits whole, file 2 does not.
		DiffBudget.Result result = DiffBudget.Apply(full, Seg1.Length);

		Assert.IsTrue(result.Truncated);
		// Only the complete first-file diff is sent; no partial second-file content.
		Assert.AreEqual(Seg1, result.PromptDiff);
		Assert.AreEqual(-1, result.PromptDiff.IndexOf("Bar.cs", StringComparison.Ordinal));
		CollectionAssert.AreEqual(fooOnly, result.IncludedFiles.ToArray());
		CollectionAssert.AreEqual(barOnly, result.OmittedFiles.ToArray());
		Assert.AreEqual(full.Length, result.FullLength);
	}

	[TestMethod]
	public void Apply_FirstFileExceedsCap_EmptyPromptAllOmitted()
	{
		string full = Seg1 + Seg2;
		DiffBudget.Result result = DiffBudget.Apply(full, Seg1.Length - 1);

		Assert.IsTrue(result.Truncated);
		Assert.AreEqual("", result.PromptDiff);
		Assert.AreEqual(0, result.IncludedFiles.Count);
		CollectionAssert.AreEqual(bothFiles, result.OmittedFiles.ToArray());
	}

	[TestMethod]
	public void Apply_StopsAtFirstOverflow_DoesNotPackLaterSmallerFiles()
	{
		// With a cap that the first (larger) file overflows, nothing is sent —
		// we stop at the first overflow rather than packing later smaller files.
		string full = Seg1 + Seg2;
		DiffBudget.Result result = DiffBudget.Apply(full, Seg1.Length - 1);

		Assert.AreEqual("", result.PromptDiff);
		Assert.AreEqual(0, result.IncludedFiles.Count);
	}

	[TestMethod]
	public void Apply_EmptyDiff_NotTruncated()
	{
		DiffBudget.Result result = DiffBudget.Apply("", 100);

		Assert.IsFalse(result.Truncated);
		Assert.AreEqual("", result.PromptDiff);
		Assert.AreEqual(0, result.FullLength);
		Assert.AreEqual(0, result.IncludedFiles.Count);
		Assert.AreEqual(0, result.OmittedFiles.Count);
	}

	[TestMethod]
	public void Apply_ExtractsFileNameFromBSidePath()
	{
		string full =
			"diff --git a/src/Deep/Path.cs b/src/Deep/Path.cs\n" +
			"index 1..2 100644\n" +
			"--- a/src/Deep/Path.cs\n" +
			"+++ b/src/Deep/Path.cs\n" +
			"@@ -1 +1 @@\n" +
			"-a\n" +
			"+b\n";
		DiffBudget.Result result = DiffBudget.Apply(full, full.Length);

		CollectionAssert.AreEqual(deepPath, result.IncludedFiles.ToArray());
	}
}
