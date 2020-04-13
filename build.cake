#tool nuget:?package=GitVersion.CommandLine&version=5.2.4
#addin nuget:?package=Cake.FileHelpers&version=3.2.0

// Environment

Context.Environment.WorkingDirectory = MakeAbsolute(new DirectoryPath("../"));

// Arguments

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Debug");
var solution = Argument<string>("solution", null);

bool deploy = HasArgument("deploy");
string nugetFeed = Argument<string>("nugetFeed", null);
string nugetApiKey = Argument<string>("nugetApiKey", null);

GitVersion version;
FilePath solutionFile;

// Configuration

var root = Context.Environment.WorkingDirectory;

var folders = new 
{
    artifacts   = root + "/artifacts/",
    solution    = root,
    apps        = root + "/apps/",
    meta        = root + "/meta/",
    src         = root + "/src/",
    packages    = root + "/artifacts/packages/",
    tests       = root + "/tests/",
    testResults = root + "/artifacts/test-results/"
};

solutionFile = GetSolutionFile(root, solution);

// Clean
Task("Clean")
    .Description("Cleans the working and build output directories")
    .Does(() =>
    {
        CleanDirectories(new DirectoryPath[] {
            folders.artifacts
        });

        CleanDirectories("./src/**/" + configuration);
        CleanDirectories("./tests/**/" + configuration);
        CleanDirectories("./samples/**/" + configuration);
    });

// Version
Task("Version")
    .Description("Generates version information")
    .Does(() => 
    {
        version = GitVersion(new GitVersionSettings
        {
            UpdateAssemblyInfo = false
        });

        var props = File("./Directory.Build.props");
        XmlPoke(props, "/Project/PropertyGroup[@Label='Project']/Version", version.NuGetVersion);
        XmlPoke(props, "/Project/PropertyGroup[@Label='Project']/InformationalVersion", version.InformationalVersion);

        Information("Version: " + version.NuGetVersion);
        Information("Product version: " + version.InformationalVersion);
    });

// Build

Task("Build")
    .Description("Builds all projects in the solution")
    .IsDependentOn("Clean")
    .IsDependentOn("Version")
    .Does(() =>
    {
        DotNetCoreBuild(solutionFile.FullPath, new DotNetCoreBuildSettings
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
        CreateDirectory(folders.artifacts + "nupkg");

        var projects = GetFiles(folders.src + "**/*.csproj");
        foreach (var project in projects)
        {
            string nuspecFile = System.IO.Path.GetFileNameWithoutExtension(project.FullPath) + ".nuspec";
            var nuspecPath = MakeAbsolute(new FilePath(System.IO.Path.Combine(project.GetDirectory().FullPath, nuspecFile)));
            var tempPath = MakeAbsolute(new FilePath(folders.artifacts + nuspecFile));

						if (FileExists(nuspecPath))
						{
                CopyFile(nuspecPath, tempPath);
                ReplaceTextInFiles(tempPath.FullPath, "$version$", version.NuGetVersion);
                ReplaceTextInFiles(tempPath.FullPath, "$configuration$", configuration);

								DotNetCorePack(project.FullPath, new DotNetCorePackSettings
								{
										ArgumentCustomization = args =>
										{
												args.Append("--include-symbols");
												args.Append("/p:SemVer=" + version.NuGetVersion);
                        args.Append("/p:NuspecFile=" + tempPath.FullPath);
                        args.Append("/p:NuspecBasePath=" + MakeAbsolute(project.GetDirectory()).FullPath);
                        return args;
										},
										Configuration = configuration,
										OutputDirectory = folders.packages,
										NoBuild = true
								});
						}
						else
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
                    OutputDirectory = folders.packages,
                    NoBuild = true
                });
						}
        }

        var specs = GetFiles(folders.meta + "**/*.nuspec");
        if (specs.Any())
        { 
            CreateDirectory(folders.artifacts + "meta/");
            CopyDirectory(folders.meta, folders.artifacts + "meta/");
            ReplaceTextInFiles(folders.artifacts + "meta/*.nuspec", "$version$", version.NuGetVersion);

            specs = GetFiles(folders.artifacts + "meta/*.nuspec");
            foreach (var spec in specs)
            {
                NuGetPack(spec, new NuGetPackSettings
                {
                    OutputDirectory = folders.artifacts
                });
            }
        }
    });

// Packaging

Task("Publish-NuGet")
    .Description("Published NuGet packages")
    .WithCriteria(() => deploy)
    .IsDependentOn("Pack")
    .Does(() =>
    {
        if (string.IsNullOrEmpty(nugetFeed) || string.IsNullOrEmpty(nugetApiKey))
        {
            Error("NuGet feed URL and API key must be provided.");
        }
        else
        {
            var packages = GetFiles(folders.packages + "*.nupkg");
            foreach (var package in packages)
            {
                NuGetPush(package, new NuGetPushSettings
                {
                    Source = nugetFeed,
                    ApiKey = nugetApiKey
                });
            }
        }
    });

// Default

Task("Default")
.IsDependentOn("Pack")
.IsDependentOn("Publish-NuGet")
;

// Execution

RunTarget(target);

public CakeSettings GetCakeSettings(ICakeContext context, IDictionary<string, string> arguments = null)
{
    var settings = new CakeSettings { Arguments = arguments };

    if (context.Environment.Runtime.IsCoreClr)
    {
        var cakePath = System.IO.Path
            .Combine(context.Environment.ApplicationRoot.FullPath, "Cake.dll")
            .Substring(Context.Environment.WorkingDirectory.FullPath.Length + 1);

        settings.ToolPath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
        settings.ArgumentCustomization = args => string.Concat(cakePath, " ", args.Render());
    }

    return settings;
}

public FilePath GetSolutionFile(DirectoryPath root, string solution)
{
    if (solution is object)
    {
        var solutionFile = root.CombineWithFilePath(solution);
        if (FileExists(solutionFile))
        {
            Information("Using solution file: " + solutionFile.FullPath);
            return solutionFile;
        }
        else
        {
            Error("Unable to resolve solution file: " + solutionFile.FullPath);
        }
    }
    else
    {
        var solutionFiles = GetFiles(root + "/*.sln");
        if (solutionFiles.Count == 1)
        {
            var solutionFile = solutionFiles.Single();
            Information("Using solution file: " + solutionFile.FullPath);
            return solutionFile;
        }
        else if (solutionFiles.Count > 1)
        {
            Error("Unable to resolve solution file, there is more than 1 solution file available at: " + root.FullPath);
        }
        else
        {
            Error("Unable to resolve solution file");
        }
    }

    return null;
}