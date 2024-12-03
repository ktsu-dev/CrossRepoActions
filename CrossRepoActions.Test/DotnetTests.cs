namespace ktsu.CrossRepoActions.Test;
using System.Collections.ObjectModel;
using System.Management.Automation;
using Moq;

[TestClass]
public class DotnetTests
{
	private readonly Mock<PowerShell> _mockPowerShell;

	public DotnetTests() => _mockPowerShell = new Mock<PowerShell>();

	[TestMethod]
	public void BuildSolution_ShouldReturnErrors_WhenBuildFails()
	{
		// Arrange
		var expectedErrors = new Collection<string> { "error CS0001: Build failed" };
		_mockPowerShell.Setup(ps => ps.InvokeAndReturnOutput())
			.Returns(expectedErrors);

		// Act
		var result = Dotnet.BuildSolution();

		// Assert
		CollectionAssert.AreEqual(expectedErrors, result);
	}

	[TestMethod]
	public void BuildProject_ShouldReturnErrors_WhenBuildFails()
	{
		// Arrange
		var projectFile = new AbsoluteFilePath("path/to/project.csproj");
		var expectedErrors = new Collection<string> { "error CS0001: Build failed" };
		_mockPowerShell.Setup(ps => ps.InvokeAndReturnOutput())
			.Returns(expectedErrors);

		// Act
		var result = Dotnet.BuildProject(projectFile);

		// Assert
		CollectionAssert.AreEqual(expectedErrors, result);
	}

	[TestMethod]
	public void RunTests_ShouldReturnTestResults()
	{
		// Arrange
		var expectedResults = new Collection<string> { "Passed Test1", "Failed Test2" };
		_mockPowerShell.Setup(ps => ps.InvokeAndReturnOutput(PowershellStreams.All))
			.Returns(expectedResults);

		// Act
		var result = Dotnet.RunTests();

		// Assert
		CollectionAssert.AreEqual(expectedResults, result);
	}

	[TestMethod]
	public void GetTests_ShouldReturnTestList()
	{
		// Arrange
		var expectedTests = new Collection<string> { "Test1", "Test2" };
		_mockPowerShell.Setup(ps => ps.InvokeAndReturnOutput())
			.Returns(expectedTests);

		// Act
		var result = Dotnet.GetTests();

		// Assert
		CollectionAssert.AreEqual(expectedTests, result);
	}

	[TestMethod]
	public void GetProjects_ShouldReturnProjectList()
	{
		// Arrange
		var solutionFile = new AbsoluteFilePath("path/to/solution.sln");
		var expectedProjects = new Collection<string> { "project1.csproj", "project2.csproj" };
		_mockPowerShell.Setup(ps => ps.InvokeAndReturnOutput())
			.Returns(expectedProjects);

		// Act
		var result = Dotnet.GetProjects(solutionFile);

		// Assert
		CollectionAssert.AreEqual(expectedProjects, result);
	}

	[TestMethod]
	public void GetSolutionDependencies_ShouldReturnDependencies()
	{
		// Arrange
		var solutionFile = new AbsoluteFilePath("path/to/solution.sln");
		var expectedDependencies = new Collection<Package>
		{
			new Package { Name = "Package1", Version = "1.0.0" },
			new Package { Name = "Package2", Version = "2.0.0" }
		};
		_mockPowerShell.Setup(ps => ps.InvokeAndReturnOutput())
			.Returns(new Collection<string> { "> Package1 1.0.0", "> Package2 2.0.0" });

		// Act
		var result = Dotnet.GetSolutionDependencies(solutionFile);

		// Assert
		CollectionAssert.AreEqual(expectedDependencies, result);
	}

	[TestMethod]
	public void GetOutdatedProjectDependencies_ShouldReturnOutdatedPackages()
	{
		// Arrange
		var projectFile = new AbsoluteFilePath("path/to/project.csproj");
		var expectedPackages = new Collection<Package>
		{
			new Package { Name = "Package1", Version = "1.0.0" },
			new Package { Name = "Package2", Version = "2.0.0" }
		};
		var jsonResult = "[{\"projects\":[{\"frameworks\":[{\"topLevelPackages\":[{\"id\":\"Package1\",\"requestedVersion\":\"1.0.0\"},{\"id\":\"Package2\",\"requestedVersion\":\"2.0.0\"}]}]}]}]";
		_mockPowerShell.Setup(ps => ps.InvokeAndReturnOutput())
			.Returns(new Collection<string> { jsonResult });

		// Act
		var result = Dotnet.GetOutdatedProjectDependencies(projectFile);

		// Assert
		CollectionAssert.AreEqual(expectedPackages, result);
	}

	[TestMethod]
	public void UpdatePackages_ShouldReturnUpdateResults()
	{
		// Arrange
		var projectFile = new AbsoluteFilePath("path/to/project.csproj");
		var packages = new Collection<Package>
		{
			new Package { Name = "Package1", Version = "1.0.0" },
			new Package { Name = "Package2", Version = "2.0.0" }
		};
		var expectedResults = new Collection<string> { "Package1 updated", "Package2 updated" };
		_mockPowerShell.Setup(ps => ps.InvokeAndReturnOutput())
			.Returns(expectedResults);

		// Act
		var result = Dotnet.UpdatePackages(projectFile, packages);

		// Assert
		CollectionAssert.AreEqual(expectedResults, result);
	}

	[TestMethod]
	public void GetProjectAssemblyName_ShouldReturnAssemblyName()
	{
		// Arrange
		var projectFile = new AbsoluteFilePath("path/to/project.csproj");
		var expectedAssemblyName = "ProjectAssembly";
		_mockPowerShell.Setup(ps => ps.InvokeAndReturnOutput())
			.Returns(new Collection<string> { expectedAssemblyName });

		// Act
		var result = Dotnet.GetProjectAssemblyName(projectFile);

		// Assert
		Assert.AreEqual(expectedAssemblyName, result);
	}

	[TestMethod]
	public void GetProjectVersion_ShouldReturnProjectVersion()
	{
		// Arrange
		var projectFile = new AbsoluteFilePath("path/to/project.csproj");
		var expectedVersion = "1.0.0";
		_mockPowerShell.Setup(ps => ps.InvokeAndReturnOutput())
			.Returns(new Collection<string> { expectedVersion });

		// Act
		var result = Dotnet.GetProjectVersion(projectFile);

		// Assert
		Assert.AreEqual(expectedVersion, result);
	}

	[TestMethod]
	public void IsProjectPackable_ShouldReturnTrue_WhenProjectIsPackable()
	{
		// Arrange
		var projectFile = new AbsoluteFilePath("path/to/project.csproj");
		_mockPowerShell.Setup(ps => ps.InvokeAndReturnOutput())
			.Returns(new Collection<string> { "true" });

		// Act
		var result = Dotnet.IsProjectPackable(projectFile);

		// Assert
		Assert.IsTrue(result);
	}

	[TestMethod]
	public void GetProjectPackage_ShouldReturnProjectPackage()
	{
		// Arrange
		var projectFile = new AbsoluteFilePath("path/to/project.csproj");
		var expectedPackage = new Package { Name = "ProjectAssembly", Version = "1.0.0" };
		_mockPowerShell.SetupSequence(ps => ps.InvokeAndReturnOutput())
			.Returns(new Collection<string> { "ProjectAssembly" })
			.Returns(new Collection<string> { "1.0.0" });

		// Act
		var result = Dotnet.GetProjectPackage(projectFile);

		// Assert
		Assert.AreEqual(expectedPackage, result);
	}

	[TestMethod]
	public void GetErrors_ShouldReturnErrors()
	{
		// Arrange
		var input = new Collection<string> { "error CS0001: Build failed", "Build succeeded" };
		var expectedErrors = new Collection<string> { "error CS0001: Build failed" };

		// Act
		var result = Dotnet.GetErrors(input);

		// Assert
		CollectionAssert.AreEqual(expectedErrors, result);
	}

	[TestMethod]
	public void DiscoverSolutionDependencies_ShouldReturnSolutions()
	{
		// Arrange
		var solutionFiles = new Collection<AbsoluteFilePath> { new AbsoluteFilePath("path/to/solution.sln") };
		var expectedSolutions = new Collection<Solution>
		{
			new Solution
			{
				Name = "Solution",
				Path = new AbsoluteFilePath("path/to/solution.sln"),
				Projects = new Collection<AbsoluteFilePath> { new AbsoluteFilePath("path/to/project.csproj") },
				Packages = new Collection<Package> { new Package { Name = "ProjectAssembly", Version = "1.0.0" } },
				Dependencies = new Collection<Package> { new Package { Name = "Package1", Version = "1.0.0" } }
			}
		};
		_mockPowerShell.Setup(ps => ps.InvokeAndReturnOutput())
			.Returns(new Collection<string> { "project.csproj", "> Package1 1.0.0" });

		// Act
		var result = Dotnet.DiscoverSolutionDependencies(solutionFiles);

		// Assert
		CollectionAssert.AreEqual(expectedSolutions, result);
	}

	[TestMethod]
	public void SortSolutionsByDependencies_ShouldReturnSortedSolutions()
	{
		// Arrange
		var solutions = new Collection<Solution>
		{
			new Solution
			{
				Name = "Solution1",
				Path = new AbsoluteFilePath("path/to/solution1.sln"),
				Projects = new Collection<AbsoluteFilePath> { new AbsoluteFilePath("path/to/project1.csproj") },
				Packages = new Collection<Package> { new Package { Name = "ProjectAssembly1", Version = "1.0.0" } },
				Dependencies = new Collection<Package> { new Package { Name = "Package1", Version = "1.0.0" } }
			},
			new Solution
			{
				Name = "Solution2",
				Path = new AbsoluteFilePath("path/to/solution2.sln"),
				Projects = new Collection<AbsoluteFilePath> { new AbsoluteFilePath("path/to/project2.csproj") },
				Packages = new Collection<Package> { new Package { Name = "ProjectAssembly2", Version = "1.0.0" } },
				Dependencies = new Collection<Package> { new Package { Name = "Package2", Version = "1.0.0" } }
			}
		};
		var expectedSortedSolutions = new Collection<Solution> { solutions[1], solutions[0] };

		// Act
		var result = Dotnet.SortSolutionsByDependencies(solutions);

		// Assert
		CollectionAssert.AreEqual(expectedSortedSolutions, result);
	}

	[TestMethod]
	public void DiscoverSolutionFiles_ShouldReturnSolutionFiles()
	{
		// Arrange
		var root = new AbsoluteDirectoryPath("path/to/root");
		var expectedSolutionFiles = new Collection<AbsoluteFilePath> { new AbsoluteFilePath("path/to/solution.sln") };
		_mockPowerShell.Setup(ps => ps.InvokeAndReturnOutput())
			.Returns(new Collection<string> { "solution.sln" });

		// Act
		var result = Dotnet.DiscoverSolutionFiles(root);

		// Assert
		CollectionAssert.AreEqual(expectedSolutionFiles, result);
	}

	[TestMethod]
	public void DiscoverSolutions_ShouldReturnSolutions()
	{
		// Arrange
		var root = new AbsoluteDirectoryPath("path/to/root");
		var expectedSolutions = new Collection<Solution>
		{
			new Solution
			{
				Name = "Solution",
				Path = new AbsoluteFilePath("path/to/solution.sln"),
				Projects = new Collection<AbsoluteFilePath> { new AbsoluteFilePath("path/to/project.csproj") },
				Packages = new Collection<Package> { new Package { Name = "ProjectAssembly", Version = "1.0.0" } },
				Dependencies = new Collection<Package> { new Package { Name = "Package1", Version = "1.0.0" } }
			}
		};
		_mockPowerShell.Setup(ps => ps.InvokeAndReturnOutput())
			.Returns(new Collection<string> { "solution.sln", "project.csproj", "> Package1 1.0.0" });

		// Act
		var result = Dotnet.DiscoverSolutions(root);

		// Assert
		CollectionAssert.AreEqual(expectedSolutions, result);
	}

	[TestMethod]
	public void IsSolutionNested_ShouldReturnTrue_WhenSolutionIsNested()
	{
		// Arrange
		var solutionPath = new AbsoluteFilePath("path/to/solution.sln");
		_mockPowerShell.Setup(ps => ps.InvokeAndReturnOutput())
			.Returns(new Collection<string> { "nestedSolution.sln" });

		// Act
		var result = Dotnet.IsSolutionNested(solutionPath);

		// Assert
		Assert.IsTrue(result);
	}
}
