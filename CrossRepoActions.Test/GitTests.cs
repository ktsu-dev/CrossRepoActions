// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.CrossRepoActions.Test;

using System;
using System.IO;

using ktsu.Semantics.Paths;
using ktsu.Semantics.Strings;

using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Guards the reason this code runs the git command line instead of binding a library, and pins the
/// query behaviour that now depends on git's exit code rather than on scanning its output for words.
/// </summary>
/// <remarks>
/// Git LFS is a clean filter plus a set of hooks, and both belong to the git command. Anything that
/// writes blobs straight into the object database skips the filter and commits the file's raw bytes,
/// which is how binary assets ended up stored verbatim in these repositories for a long time despite
/// matching an LFS pattern. These tests pin the behaviour down rather than trusting it.
/// </remarks>
[TestClass]
public sealed class GitTests
{
	private const string LfsPointerPrefix = "version https://git-lfs.github.com/spec/v1";

	private static bool IsLfsAvailable() => Cli.Run("git", "lfs", "version").Succeeded;

	private static AbsoluteDirectoryPath CreateRepository(bool trackBinariesWithLfs)
	{
		string root = Path.Combine(Path.GetTempPath(), $"ktsu_cra_{Guid.NewGuid():N}");
		_ = Directory.CreateDirectory(root);

		AbsoluteDirectoryPath repo = root.As<AbsoluteDirectoryPath>();

		Assert.IsTrue(Cli.Run("git", "init", root).Succeeded, "git init failed.");

		// Scope identity to this throwaway repository so the test never depends on, or disturbs,
		// whatever global configuration the machine happens to carry.
		Assert.IsTrue(GitCli.Run(repo, "config", "user.name", "CrossRepoActions").Succeeded);
		Assert.IsTrue(GitCli.Run(repo, "config", "user.email", "CrossRepoActions@ktsu.dev").Succeeded);

		if (trackBinariesWithLfs)
		{
			// --local keeps the filter and hooks scoped to this repository too.
			Assert.IsTrue(GitCli.Run(repo, "lfs", "install", "--local").Succeeded, "git lfs install failed.");
			File.WriteAllText(Path.Combine(root, ".gitattributes"), "*.bin filter=lfs diff=lfs merge=lfs -text\n");
		}

		return repo;
	}

	[TestMethod]
	public void CommittingAnLfsTrackedFileStoresAPointerRatherThanRawBytes()
	{
		if (!IsLfsAvailable())
		{
			Assert.Inconclusive("git-lfs is not installed, so the clean filter cannot run.");
			return;
		}

		AbsoluteDirectoryPath repo = CreateRepository(trackBinariesWithLfs: true);

		try
		{
			// Bytes that are unmistakably not text, so a raw commit would be obvious.
			byte[] payload = new byte[2048];
			for (int i = 0; i < payload.Length; i++)
			{
				payload[i] = (byte)(i % 256);
			}

			File.WriteAllBytes(Path.Combine(repo.ToString(), "asset.bin"), payload);

			_ = Git.StageAll(repo).ToList();
			_ = Git.Commit(repo, "Add asset.bin").ToList();

			CommandResult blob = GitCli.Run(repo, "cat-file", "-p", "HEAD:asset.bin");

			Assert.IsTrue(blob.Succeeded, $"git cat-file failed: {blob.FailureText}");
			Assert.StartsWith(LfsPointerPrefix, blob.OutputText, "The committed blob should be an LFS pointer, not the file's bytes.");
			Assert.Contains("size 2048", blob.OutputText);
		}
		finally
		{
			TryDeleteDirectory(repo.ToString());
		}
	}

	[TestMethod]
	public void CommittingAFileOutsideAnyLfsPatternStoresItVerbatim()
	{
		if (!IsLfsAvailable())
		{
			Assert.Inconclusive("git-lfs is not installed, so the clean filter cannot run.");
			return;
		}

		AbsoluteDirectoryPath repo = CreateRepository(trackBinariesWithLfs: true);

		try
		{
			// The pattern covers *.bin only, so this must survive verbatim. Without this half of
			// the pair, a runner that turned everything into a pointer would still pass.
			File.WriteAllText(Path.Combine(repo.ToString(), "notes.txt"), "plain content\n");

			_ = Git.StageAll(repo).ToList();
			_ = Git.Commit(repo, "Add notes.txt").ToList();

			CommandResult blob = GitCli.Run(repo, "cat-file", "-p", "HEAD:notes.txt");

			Assert.IsTrue(blob.Succeeded, $"git cat-file failed: {blob.FailureText}");
			Assert.AreEqual("plain content", blob.OutputText);
		}
		finally
		{
			TryDeleteDirectory(repo.ToString());
		}
	}

	[TestMethod]
	public void StatusSummaryReportsCleanForACommittedWorkingTree()
	{
		AbsoluteDirectoryPath repo = CreateRepository(trackBinariesWithLfs: false);

		try
		{
			File.WriteAllText(Path.Combine(repo.ToString(), "notes.txt"), "content\n");
			_ = Git.StageAll(repo).ToList();
			_ = Git.Commit(repo, "Add notes.txt").ToList();

			Assert.AreEqual("clean", Git.GetStatusSummary(repo));

			File.WriteAllText(Path.Combine(repo.ToString(), "notes.txt"), "changed\n");

			Assert.AreNotEqual("clean", Git.GetStatusSummary(repo));
		}
		finally
		{
			TryDeleteDirectory(repo.ToString());
		}
	}

	[TestMethod]
	public void StatusSummaryReportsUnknownOutsideARepository()
	{
		string outside = Path.Combine(Path.GetTempPath(), $"ktsu_norepo_{Guid.NewGuid():N}");
		_ = Directory.CreateDirectory(outside);

		try
		{
			// git exits non-zero here. The summary must not claim the directory is clean, because
			// callers treat "clean" as "nothing to do".
			Assert.AreEqual("unknown", Git.GetStatusSummary(outside.As<AbsoluteDirectoryPath>()));
		}
		finally
		{
			TryDeleteDirectory(outside);
		}
	}

	[TestMethod]
	public void UntrackedFilesAreDetected()
	{
		AbsoluteDirectoryPath repo = CreateRepository(trackBinariesWithLfs: false);

		try
		{
			Assert.IsFalse(Git.HasUntrackedFiles(repo));

			File.WriteAllText(Path.Combine(repo.ToString(), "stray.txt"), "content\n");

			Assert.IsTrue(Git.HasUntrackedFiles(repo));
		}
		finally
		{
			TryDeleteDirectory(repo.ToString());
		}
	}

	[TestMethod]
	public void AbsentUpstreamIsReportedAsNullRatherThanAsText()
	{
		AbsoluteDirectoryPath repo = CreateRepository(trackBinariesWithLfs: false);

		try
		{
			File.WriteAllText(Path.Combine(repo.ToString(), "notes.txt"), "content\n");
			_ = Git.StageAll(repo).ToList();
			_ = Git.Commit(repo, "Add notes.txt").ToList();

			// A throwaway repository has no remote, so git fails and its message must not be
			// mistaken for a branch name.
			Assert.IsNull(Git.GetUpstreamBranch(repo));
			Assert.IsNull(Git.GetUpstreamAheadBehind(repo));
			Assert.IsNull(Git.GetDefaultBranch(repo));
		}
		finally
		{
			TryDeleteDirectory(repo.ToString());
		}
	}

	[TestMethod]
	public void RefExistenceFollowsTheExitCode()
	{
		AbsoluteDirectoryPath repo = CreateRepository(trackBinariesWithLfs: false);

		try
		{
			File.WriteAllText(Path.Combine(repo.ToString(), "notes.txt"), "content\n");
			_ = Git.StageAll(repo).ToList();
			_ = Git.Commit(repo, "Add notes.txt").ToList();

			Assert.IsTrue(Git.RefExists(repo, "HEAD"));
			Assert.IsFalse(Git.RefExists(repo, "refs/remotes/origin/nonexistent"));
		}
		finally
		{
			TryDeleteDirectory(repo.ToString());
		}
	}

	[TestMethod]
	public void CurrentBranchIsReadFromAFreshRepository()
	{
		AbsoluteDirectoryPath repo = CreateRepository(trackBinariesWithLfs: false);

		try
		{
			File.WriteAllText(Path.Combine(repo.ToString(), "notes.txt"), "content\n");
			_ = Git.StageAll(repo).ToList();
			_ = Git.Commit(repo, "Add notes.txt").ToList();

			Assert.IsNotEmpty(Git.GetCurrentBranch(repo));
			Assert.AreNotEqual("unknown", Git.GetCurrentBranch(repo));
		}
		finally
		{
			TryDeleteDirectory(repo.ToString());
		}
	}

	[TestMethod]
	public void FullDiffKeepsTheLeadingColumnThatGivesEachLineItsMeaning()
	{
		AbsoluteDirectoryPath repo = CreateRepository(trackBinariesWithLfs: false);

		try
		{
			string file = Path.Combine(repo.ToString(), "notes.txt");
			File.WriteAllText(file, "first\nsecond\nthird\n");
			_ = Git.StageAll(repo).ToList();
			_ = Git.Commit(repo, "Add notes.txt").ToList();

			File.WriteAllText(file, "first\nCHANGED\nthird\n");

			string diff = Git.GetFullDiff(repo);

			// Trimming each line, as the PowerShell host did, strips the space that marks a context
			// line and the +/- that marks a change, leaving something no longer a valid diff.
			Assert.Contains("\n+CHANGED", diff);
			Assert.Contains("\n-second", diff);
			Assert.Contains("\n first", diff);
		}
		finally
		{
			TryDeleteDirectory(repo.ToString());
		}
	}

	[TestMethod]
	public void ArgumentsSurvivePathsContainingSpaces()
	{
		AbsoluteDirectoryPath repo = CreateRepository(trackBinariesWithLfs: false);

		try
		{
			string nested = Path.Combine(repo.ToString(), "a directory with spaces");
			_ = Directory.CreateDirectory(nested);

			string file = Path.Combine(nested, "a file with spaces.txt");
			File.WriteAllText(file, "content\n");

			_ = Git.StageAll(repo).ToList();
			_ = Git.Commit(repo, "Add a file with spaces").ToList();

			// Passing each argument separately is what makes this work; a single command string
			// split on whitespace would break every one of these paths.
			CommandResult blob = GitCli.Run(repo, "cat-file", "-p", "HEAD:a directory with spaces/a file with spaces.txt");

			Assert.IsTrue(blob.Succeeded, $"git cat-file failed: {blob.FailureText}");
			Assert.AreEqual("content", blob.OutputText);
		}
		finally
		{
			TryDeleteDirectory(repo.ToString());
		}
	}

	private static void TryDeleteDirectory(string path)
	{
		try
		{
			// Git marks objects read-only, which blocks a plain recursive delete on Windows.
			foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
			{
				File.SetAttributes(file, FileAttributes.Normal);
			}

			Directory.Delete(path, recursive: true);
		}
		catch (IOException)
		{
			// Covers a missing directory too. A best-effort cleanup of a temp directory is not
			// worth failing a test over.
		}
		catch (UnauthorizedAccessException)
		{
			// As above.
		}
	}
}
