#tool "nuget:?package=GitVersion.CommandLine&version=5.1.2"

// Arguments

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Debug");
GitVersion version;

// Configuration

var folders = new 
{
    artifacts = "../artifacts/",
    solution = "../",
    src = "../src/",
    tests = "../tests/",
    testResults = "../artifacts/test-results/"
};

// Clean
Task("Clean")
    .Description("Cleans the working and build output directories")
    .Does(() =>
    {
        CleanDirectories(new DirectoryPath[] {
            folders.artifacts
        });

        CleanDirectories("../src/**/" + configuration);
        CleanDirectories("../tests/**/" + configuration);
        CleanDirectories("../samples/**/" + configuration);
    });

// Version
Task("Version")
    .Description("Generates version information")
    .Does(() => 
    {
        version = GitVersion(new GitVersionSettings
        {
            RepositoryPath = "../",
            UpdateAssemblyInfo = false
        });
    });

// Build

Task("Build")
    .Description("Builds all projects in the solution")
    .IsDependentOn("Clean")
    .IsDependentOn("Version")
    .Does(() =>
    {
        DotNetCoreBuild(folders.solution, new DotNetCoreBuildSettings
        {
            Configuration = configuration,
            ArgumentCustomization = args =>
            {
                args.Append("/p:SemVer=" + version.NuGetVersion);
                return args;
            }
        });
    });

// Test

Task("Test")
    .Description("Runs unit tests")
    .IsDependentOn("Build")
    .Does(() =>
    {
        var tests = GetFiles(folders.tests + "**/*.csproj");
        foreach (var test in tests)
        {
            string folder = System.IO.Path.GetDirectoryName(test.FullPath);
            string project = folder.Substring(folder.LastIndexOf('\\') + 1);
            string resultsFile = project + ".xml";
            string fullResultsFile = System.IO.Path.Combine(folders.testResults, resultsFile);

            CreateDirectory(folders.testResults);
            
            var settings = new DotNetCoreTestSettings
            {
                Configuration = configuration,
                NoBuild = true,
                NoRestore = true,
                ResultsDirectory = folders.testResults,
                Logger = "trx;LogFilename=" + resultsFile
            };
            
            DotNetCoreTest(test.FullPath, settings);
        }

        if (AppVeyor.IsRunningOnAppVeyor)
        {
            foreach (var result in GetFiles(folders.testResults + "**/*.xml")) 
            {
                AppVeyor.UploadTestResults(result.FullPath, AppVeyorTestResultsType.MSTest);
            }
        }
    });

// Packaging

Task("Pack")
    .Description("Packs the output of projects")
    .IsDependentOn("Test")
    .Does(() =>
    {
        var projects = GetFiles(folders.src + "**/*.csproj");
        foreach (var project in projects)
        {
            DotNetCorePack(project.FullPath, new DotNetCorePackSettings
            {
                ArgumentCustomization = args =>
                {
                    args.Append("--include-symbols");
                    args.Append("/p:SemVer=" + version.NuGetVersion);
                    return args;
                },
                Configuration = configuration,
                OutputDirectory = folders.artifacts,
                NoBuild = true
            });
        }
    });

// Default

Task("Default")
.IsDependentOn("Pack")
;

// Execution

RunTarget(target);