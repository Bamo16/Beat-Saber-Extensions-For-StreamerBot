using CommandLine;

namespace BuildBeatSaberExtensions.Models;

public class CommandLineOptions
{
    [Option('p', "path", Required = true, HelpText = "Path to the C# project to process.")]
    public string Path { get; set; }
}
