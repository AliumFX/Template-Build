// Arguments

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Debug");

// Configuration

var folders = new 
{
    solution = "../",
    src = "../src/",
    test = "../test/"
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

// Default

Task("Default")
.IsDependentOn("Build");

// Execution

RunTarget(target);