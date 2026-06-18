using CommandLine;

namespace StreamerBotBuilder.Models;

public class CommandLineOptions
{
    [Option(
        'p',
        "path",
        Required = false,
        Default = null,
        HelpText = "Path to the C# project root. Defaults to current directory."
    )]
    public string Path { get; set; }

    [Option(
        'c',
        "copy-to-clipboard",
        Required = false,
        Default = false,
        HelpText = "Copy merged output to clipboard. Overrides BuildConfig."
    )]
    public bool CopyToClipboard { get; set; }

    [Option(
        'o',
        "output-file",
        Required = false,
        Default = null,
        HelpText = "Write merged output to this file path. Overrides BuildConfig."
    )]
    public string OutputFile { get; set; }
}
