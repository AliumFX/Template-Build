// Arguments

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Debug");

// Configuration

var folders = new 
{
    build = "../build/",
    solution = "../",
    src = "../src/",
    test = "../tests/"
};

Task("Restore")
.Does(() =>
{
    DotNetCoreRestore(folders.solution);
});

// Build

Task("Build")
.IsDependentOn("Restore")
.Does(() =>
{
    DotNetCoreBuild(folders.solution, new DotNetCoreBuildSettings
    {
        Configuration = configuration
    });
});

// Test

Task("Test")
.IsDependentOn("Build")
.Does(() =>
{
    var tests = GetFiles(folders.test + "**/*.csproj");
    foreach (var test in tests)
    {
        string folder = System.IO.Path.GetDirectoryName(test.FullPath);
        string project = folder.Substring(folder.LastIndexOf('\\') + 1);
        string resultsFile = folders.build + "test-results/" + project + ".xml";

        DotNetCoreTest(test.FullPath, new DotNetCoreTestSettings
        {
            ArgumentCustomization = args => args.Append("--xml " + resultsFile),
            Configuration = configuration
        });
    }
});

// Default

Task("Default")
.IsDependentOn("Build")
//.IsDependentOn("Test")
;

// Execution

RunTarget(target);