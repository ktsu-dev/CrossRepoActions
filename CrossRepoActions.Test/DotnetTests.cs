//namespace ktsu.CrossRepoActions.Test;
//using System.Collections.ObjectModel;
//using System.Management.Automation;
//using ktsu.Extensions;
//using ktsu.StrongPaths;
//using Moq;

//[TestClass]
//public class DotnetTests
//{
//	private readonly Mock<PowerShell> mockPowerShell;

//	public DotnetTests() => mockPowerShell = new Mock<PowerShell>();

//	[TestMethod]
//	public void BuildSolution_ShouldReturnErrors_WhenBuildFails()
//	{
//		// Arrange
//		var expectedErrors = new Collection<string> { "error CS0001: Build failed" };
//		mockPowerShell.Setup(ps => ps.InvokeAndReturnOutput(PowershellStreams.Default))
//			.Returns(expectedErrors);

//		// Act
//		var result = Dotnet.BuildSolution();

//		// Assert
//		CollectionAssert.AreEqual(expectedErrors, result);
//	}

//	[TestMethod]
//	public void BuildProject_ShouldReturnErrors_WhenBuildFails()
//	{
//		// Arrange
//		var projectFile = "path/to/project.csproj".As<AbsoluteFilePath>();
//		var expectedErrors = new Collection<string> { "error CS0001: Build failed" };
//		mockPowerShell.Setup(ps => ps.InvokeAndReturnOutput(PowershellStreams.Default))
//			.Returns(expectedErrors);

//		// Act
//		var result = Dotnet.BuildProject(projectFile);

//		// Assert
//		CollectionAssert.AreEqual(expectedErrors, result);
//	}

//	[TestMethod]
//	public void RunTests_ShouldReturnTestResults()
//	{
//		// Arrange
//		var expectedResults = new Collection<string> { "Passed Test1", "Failed Test2" };
//		mockPowerShell.Setup(ps => ps.InvokeAndReturnOutput(PowershellStreams.All))
//			.Returns(expectedResults);

//		// Act
//		var result = Dotnet.RunTests();

//		// Assert
//		CollectionAssert.AreEqual(expectedResults, result);
//	}

//	[TestMethod]
//	public void GetTests_ShouldReturnTestList()
//	{
//		// Arrange
//		var expectedTests = new Collection<string> { "Test1", "Test2" };
//		mockPowerShell.Setup(ps => ps.InvokeAndReturnOutput(PowershellStreams.Default))
//			.Returns(expectedTests);

//		// Act
//		var result = Dotnet.GetTests();

//		// Assert
//		CollectionAssert.AreEqual(expectedTests, result);
//	}

//	[TestMethod]
//	public void GetProjects_ShouldReturnProjectList()
//	{
//		// Arrange
//		var solutionFile = "path/to/solution.sln".As<AbsoluteFilePath>();
//		var expectedProjects = new Collection<string> { "project1.csproj", "project2.csproj" };
//		mockPowerShell.Setup(ps => ps.InvokeAndReturnOutput(PowershellStreams.Default))
//			.Returns(expectedProjects);

//		// Act
//		var result = Dotnet.GetProjects(solutionFile);

//		// Assert
//		CollectionAssert.AreEqual(expectedProjects, result);
//	}

//	[TestMethod]
//	public void GetSolutionDependencies_ShouldReturnDependencies()
//	{
//		// Arrange
//		var solutionFile = "path/to/solution.sln".As<AbsoluteFilePath>();
//		var expectedDependencies = new Collection<Package>
//		{
//			new() { Name = "Package1", Version = "1.0.0" },
//			new() { Name = "Package2", Version = "2.0.0" }
//		};
//		mockPowerShell.Setup(ps => ps.InvokeAndReturnOutput(PowershellStreams.Default))
//			.Returns(["> Package1 1.0.0", "> Package2 2.0.0"]);

//		// Act
//		var result = Dotnet.GetSolutionDependencies(solutionFile);

//		// Assert
//		CollectionAssert.AreEqual(expectedDependencies, result);
//	}

//	[TestMethod]
//	public void GetOutdatedProjectDependencies_ShouldReturnOutdatedPackages()
//	{
//		// Arrange
//		var projectFile = "path/to/project.csproj".As<AbsoluteFilePath>();
//		var expectedPackages = new Collection<Package>
//		{
//			new() { Name = "Package1", Version = "1.0.0" },
//			new() { Name = "Package2", Version = "2.0.0" }
//		};
//		string jsonResult = /*lang=json,strict*/ "[{\"projects\":[{\"frameworks\":[{\"topLevelPackages\":[{\"id\":\"Package1\",\"requestedVersion\":\"1.0.0\"},{\"id\":\"Package2\",\"requestedVersion\":\"2.0.0\"}]}]}]}]";
//		mockPowerShell.Setup(ps => ps.InvokeAndReturnOutput(PowershellStreams.Default))
//			.Returns([jsonResult]);

//		// Act
//		var result = Dotnet.GetOutdatedProjectDependencies(projectFile);

//		// Assert
//		CollectionAssert.AreEqual(expectedPackages, result);
//	}

//	[TestMethod]
//	public void UpdatePackages_ShouldReturnUpdateResults()
//	{
//		// Arrange
//		var projectFile = "path/to/project.csproj".As<AbsoluteFilePath>();
//		var packages = new Collection<Package>
//		{
//			new() { Name = "Package1", Version = "1.0.0" },
//			new() { Name = "Package2", Version = "2.0.0" }
//		};
//		var expectedResults = new Collection<string> { "Package1 updated", "Package2 updated" };
//		mockPowerShell.Setup(ps => ps.InvokeAndReturnOutput(PowershellStreams.Default))
//			.Returns(expectedResults);

//		// Act
//		var result = Dotnet.UpdatePackages(projectFile, packages);

//		// Assert
//		CollectionAssert.AreEqual(expectedResults, result);
//	}

//	[TestMethod]
//	public void GetProjectAssemblyName_ShouldReturnAssemblyName()
//	{
//		// Arrange
//		var projectFile = "path/to/project.csproj".As<AbsoluteFilePath>();
//		string expectedAssemblyName = "ProjectAssembly";
//		mockPowerShell.Setup(ps => ps.InvokeAndReturnOutput(PowershellStreams.Default))
//			.Returns([expectedAssemblyName]);

//		// Act
//		string result = Dotnet.GetProjectAssemblyName(projectFile);

//		// Assert
//		Assert.AreEqual(expectedAssemblyName, result);
//	}

//	[TestMethod]
//	public void GetProjectVersion_ShouldReturnProjectVersion()
//	{
//		// Arrange
//		var projectFile = "path/to/project.csproj".As<AbsoluteFilePath>();
//		string expectedVersion = "1.0.0";
//		mockPowerShell.Setup(ps => ps.InvokeAndReturnOutput(PowershellStreams.Default))
//			.Returns([expectedVersion]);

//		// Act
//		string result = Dotnet.GetProjectVersion(projectFile);

//		// Assert
//		Assert.AreEqual(expectedVersion, result);
//	}

//	[TestMethod]
//	public void IsProjectPackable_ShouldReturnTrue_WhenProjectIsPackable()
//	{
//		// Arrange
//		var projectFile = "path/to/project.csproj".As<AbsoluteFilePath>();
//		mockPowerShell.Setup(ps => ps.InvokeAndReturnOutput(PowershellStreams.Default))
//			.Returns(["true"]);

//		// Act
//		bool result = Dotnet.IsProjectPackable(projectFile);

//		// Assert
//		Assert.IsTrue(result);
//	}

//	[TestMethod]
//	public void GetProjectPackage_ShouldReturnProjectPackage()
//	{
//		// Arrange
//		var projectFile = "path/to/project.csproj".As<AbsoluteFilePath>();
//		var expectedPackage = new Package { Name = "ProjectAssembly", Version = "1.0.0" };
//		mockPowerShell.SetupSequence(ps => ps.InvokeAndReturnOutput(PowershellStreams.Default))
//			.Returns(["ProjectAssembly"])
//			.Returns(["1.0.0"]);

//		// Act
//		var result = Dotnet.GetProjectPackage(projectFile);

//		// Assert
//		Assert.AreEqual(expectedPackage, result);
//	}

//	[TestMethod]
//	public void GetErrors_ShouldReturnErrors()
//	{
//		// Arrange
//		var input = new Collection<string> { "error CS0001: Build failed", "Build succeeded" };
//		var expectedErrors = new Collection<string> { "error CS0001: Build failed" };

//		// Act
//		var result = Dotnet.GetErrors(input);

//		// Assert
//		CollectionAssert.AreEqual(expectedErrors, result);
//	}

//	[TestMethod]
//	public void DiscoverSolutionDependencies_ShouldReturnSolutions()
//	{
//		// Arrange
//		var solutionFiles = new Collection<AbsoluteFilePath> { "path/to/solution.sln".As<AbsoluteFilePath>() };
//		var expectedSolutions = new Collection<Solution>
//		{
//			new() {
//				Name = "Solution",
//				Path = "path/to/solution.sln".As<AbsoluteFilePath>(),
//				Projects = ["path/to/project.csproj".As<AbsoluteFilePath>()],
//				Packages = [new() { Name = "ProjectAssembly", Version = "1.0.0" }],
//				Dependencies = [new() { Name = "Package1", Version = "1.0.0" }]
//			}
//		};
//		mockPowerShell.Setup(ps => ps.InvokeAndReturnOutput(PowershellStreams.Default))
//			.Returns(["project.csproj", "> Package1 1.0.0"]);

//		// Act
//		var result = Dotnet.DiscoverSolutionDependencies(solutionFiles);

//		// Assert
//		CollectionAssert.AreEqual(expectedSolutions, result);
//	}

//	[TestMethod]
//	public void SortSolutionsByDependencies_ShouldReturnSortedSolutions()
//	{
//		// Arrange
//		var solutions = new Collection<Solution>
//		{
//			new() {
//				Name = "Solution1",
//				Path = "path/to/solution1.sln".As<AbsoluteFilePath>(),
//				Projects = ["path/to/project1.csproj".As<AbsoluteFilePath>()],
//				Packages = [new() { Name = "ProjectAssembly1", Version = "1.0.0" }],
//				Dependencies = [new() { Name = "Package1", Version = "1.0.0" }]
//			},
//			new() {
//				Name = "Solution2",
//				Path = "path/to/solution2.sln".As < AbsoluteFilePath >(),
//				Projects = ["path/to/project2.csproj".As<AbsoluteFilePath>()],
//				Packages = [new() { Name = "ProjectAssembly2", Version = "1.0.0" }],
//				Dependencies = [new() { Name = "Package2", Version = "1.0.0" }]
//			}
//		};
//		var expectedSortedSolutions = new Collection<Solution> { solutions[1], solutions[0] };

//		// Act
//		var result = Dotnet.SortSolutionsByDependencies(solutions);

//		// Assert
//		CollectionAssert.AreEqual(expectedSortedSolutions, result);
//	}

//	[TestMethod]
//	public void DiscoverSolutionFiles_ShouldReturnSolutionFiles()
//	{
//		// Arrange
//		var root = "path/to/root".As<AbsoluteDirectoryPath>();
//		var expectedSolutionFiles = new Collection<AbsoluteFilePath> { "path/to/solution.sln".As<AbsoluteFilePath>() };
//		mockPowerShell.Setup(ps => ps.InvokeAndReturnOutput(PowershellStreams.Default))
//			.Returns(["solution.sln"]);

//		// Act
//		var result = Dotnet.DiscoverSolutionFiles(root);

//		// Assert
//		CollectionAssert.AreEqual(expectedSolutionFiles, result);
//	}

//	[TestMethod]
//	public void DiscoverSolutions_ShouldReturnSolutions()
//	{
//		// Arrange
//		var root = "path/to/root".As<AbsoluteDirectoryPath>();
//		var expectedSolutions = new Collection<Solution>
//		{
//			new() {
//				Name = "Solution",
//				Path = "path/to/solution.sln".As<AbsoluteFilePath>(),
//				Projects = ["path/to/project.csproj".As<AbsoluteFilePath>()],
//				Packages = [new() { Name = "ProjectAssembly", Version = "1.0.0" }],
//				Dependencies = [new() { Name = "Package1", Version = "1.0.0" }]
//			}
//		};
//		mockPowerShell.Setup(ps => ps.InvokeAndReturnOutput(PowershellStreams.Default))
//			.Returns(["solution.sln", "project.csproj", "> Package1 1.0.0"]);

//		// Act
//		var result = Dotnet.DiscoverSolutions(root);

//		// Assert
//		CollectionAssert.AreEqual(expectedSolutions, result);
//	}

//	[TestMethod]
//	public void IsSolutionNested_ShouldReturnTrue_WhenSolutionIsNested()
//	{
//		// Arrange
//		var solutionPath = "path/to/solution.sln".As<AbsoluteFilePath>();
//		mockPowerShell.Setup(ps => ps.InvokeAndReturnOutput(PowershellStreams.Default))
//			.Returns(["nestedSolution.sln"]);

//		// Act
//		bool result = Dotnet.IsSolutionNested(solutionPath);

//		// Assert
//		Assert.IsTrue(result);
//	}
//}
