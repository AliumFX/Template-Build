// Arguments

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Debug");

// Configuration

var folders = new 
{
    build = "../build/",
    solution = "../",
    src = "../src/",
    tests = "../tests/"
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
            string resultsFile = folders.build + "test-results/" + project + ".xml";

            DotNetCoreTest(test.FullPath, new DotNetCoreTestSettings
            {
                ArgumentCustomization = args => args.Append("--xml " + resultsFile),
                Configuration = configuration,
                NoBuild = true
            });
        }
    });

Task("Publish")
    .Description("Publishes the output of projects")
    .IsDependentOn("Build")
    .Does(() =>
    {

    });

Task("Pack")
    .Description("Packs the output of projects")
    .IsDependentOn("Publish")
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