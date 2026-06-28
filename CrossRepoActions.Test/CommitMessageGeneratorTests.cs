// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CrossRepoActions.Test;

using System;
using System.Threading;
using System.Threading.Tasks;

using ktsu.CrossRepoActions.Llm;

using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class CommitMessageGeneratorTests
{
	private sealed class FakeLlmService : ILlmService
	{
		public string? CapturedSystem { get; private set; }
		public string? CapturedUser { get; private set; }
		public string Response { get; set; } = "";

		public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
		{
			CapturedSystem = systemPrompt;
			CapturedUser = userPrompt;
			return Task.FromResult(Response);
		}
	}

	[TestMethod]
	public void BuildUserPrompt_IncludesStatAndDiff()
	{
		string prompt = CommitMessageGenerator.BuildUserPrompt("1 file changed", "diff --git a b", truncated: false);
		StringAssert.Contains(prompt, "1 file changed");
		StringAssert.Contains(prompt, "diff --git a b");
	}

	[TestMethod]
	public void BuildUserPrompt_NotesTruncation_WhenTruncated()
	{
		string prompt = CommitMessageGenerator.BuildUserPrompt("stat", "diff", truncated: true);
		StringAssert.Contains(prompt, "truncated");
	}

	[TestMethod]
	public void BuildUserPrompt_OmitsTruncationNote_WhenNotTruncated()
	{
		string prompt = CommitMessageGenerator.BuildUserPrompt("stat", "diff", truncated: false);
		Assert.AreEqual(-1, prompt.IndexOf("truncated", StringComparison.OrdinalIgnoreCase));
	}

	[TestMethod]
	public void CleanResponse_StripsCodeFences()
	{
		string cleaned = CommitMessageGenerator.CleanResponse("```\nfeat: add thing\n```");
		Assert.AreEqual("feat: add thing", cleaned);
	}

	[TestMethod]
	public void CleanResponse_StripsCodeFencesWithLanguageHint()
	{
		string cleaned = CommitMessageGenerator.CleanResponse("```text\nfix: bug\n```");
		Assert.AreEqual("fix: bug", cleaned);
	}

	[TestMethod]
	public void CleanResponse_StripsWrappingQuotes()
	{
		string cleaned = CommitMessageGenerator.CleanResponse("\"fix: correct bug\"");
		Assert.AreEqual("fix: correct bug", cleaned);
	}

	[TestMethod]
	public void CleanResponse_TrimsWhitespace()
	{
		string cleaned = CommitMessageGenerator.CleanResponse("  chore: tidy up \n");
		Assert.AreEqual("chore: tidy up", cleaned);
	}

	[TestMethod]
	public async Task GenerateAsync_ReturnsCleanedResponse_AndSendsBothPrompts()
	{
		FakeLlmService fake = new() { Response = "```\nfeat: new feature\n```" };
		CommitMessageGenerator generator = new(fake);

		string message = await generator.GenerateAsync("stat", "diff body", truncated: false).ConfigureAwait(false);

		Assert.AreEqual("feat: new feature", message);
		Assert.IsNotNull(fake.CapturedSystem);
		StringAssert.Contains(fake.CapturedUser, "diff body");
	}
}
