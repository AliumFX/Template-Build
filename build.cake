#tool "nuget:?package=GitVersion.CommandLine"

// Arguments

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Debug");
GitVersion version;

// Configuration

var folders = new 
{
    build = "../build/",
    solution = "../",
    src = "../src/",
    tests = "../tests/",
    testResults = "../build/test-results/"
};

// Clean
Task("Clean")
    .Description("Cleans the working and build output directories")
    .Does(() =>
    {
        CleanDirectories(new DirectoryPath[] {
            folders.build
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

            CreateDirectory(folders.testResults);
            
            var settings = new DotNetCoreTestSettings
            {
                NoBuild = false,
                NoRestore = true,
                ResultsDirectory = folders.testResults,
                Logger = "trx;LogFilename=" + resultsFile
            };
            
            DotNetCoreTest(test.FullPath, settings);

            if (AppVeyor.IsRunningOnAppVeyor)
            {
                AppVeyor.UploadTestResults(resultsFile, AppVeyorTestResultsType.MSTest);
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
                OutputDirectory = folders.build,
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