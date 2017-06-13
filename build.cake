// Arguments

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Debug");

// Default

Task("Default")
.Does(() =>
{
    Console.WriteLine("Build complete.");
});