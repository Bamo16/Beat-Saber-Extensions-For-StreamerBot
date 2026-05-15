using BuildBeatSaberExtensions.Models;
using CommandLine;
using TextCopy;

namespace BuildBeatSaberExtensions;

public class Program
{
    static void Main(string[] args) =>
        Parser
            .Default.ParseArguments<CommandLineOptions>(args)
            .WithNotParsed(errors => Environment.Exit(1))
            .WithParsed(opts => ProcessProject(BuildConfig.LoadBuildConfig(opts.Path)));

    private static void ProcessProject(BuildConfig config)
    {
        var project = new StreamerBotProject(config);

        if (project is { BuildSuccess: false, Errors: { Count: { } count } errors })
        {
            var message = $"Build failed with {count} compilation errors.";
            Console.WriteLine($"{message}\n");

            foreach (var error in errors)
                Console.WriteLine(error);

            return;
        }

        if (config is { OutputFileName: { } output })
            File.WriteAllText(output, project.Code);

        if (config is { CopyToClipboard: true })
            ClipboardService.SetText(project.Code);

        var actionText = config switch
        {
            { CopyToClipboard: true, OutputFileName: { } fileName } =>
                $"copied to clipboard and written to {fileName}",
            { CopyToClipboard: true } => "copied to clipboard",
            { OutputFileName: { } fileName } => $"written to {fileName}",
            _ => "generated",
        };

        Console.WriteLine($"Build completed successfully. {project.LineCount} lines {actionText}.");
    }
}
