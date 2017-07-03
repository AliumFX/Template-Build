// Arguments

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Debug");

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

// Restore

Task("Restore-NuGet")
    .Description("Restores NuGet packages")
    .Does(() =>
    {
        DotNetCoreRestore(folders.solution);
    });

// Build

Task("Build")
    .Description("Builds all projects in the solution")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore-NuGet")
    .Does(() =>
    {
        DotNetCoreBuild(folders.solution, new DotNetCoreBuildSettings
        {
            Configuration = configuration
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
            string resultsFile = folders.testResults + project + ".xml";
            string toolArguments = "xunit -xml ../" + resultsFile + " --no-build -internaldiagnostics";

            CreateDirectory(folders.testResults);
            using (var process = StartAndReturnProcess("dotnet", new ProcessSettings 
                {
                    Arguments = "xunit -xml ../" + resultsFile + " --no-build",
                    WorkingDirectory = folder
                }))
            {
                process.WaitForExit();

                if (AppVeyor.IsRunningOnAppVeyor)
                {
                    AppVeyor.UploadTestResults(resultsFile, AppVeyorTestResultsType.XUnit);
                }
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