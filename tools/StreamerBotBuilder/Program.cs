using CommandLine;
using StreamerBotBuilder.Models;
using TextCopy;

namespace StreamerBotBuilder;

public class Program
{
    static void Main(string[] args) =>
        Parser
            .Default.ParseArguments<CommandLineOptions>(args)
            .WithNotParsed(errors => Environment.Exit(1))
            .WithParsed(opts =>
            {
                var path = opts.Path ?? Directory.GetCurrentDirectory();
                ProcessProject(BuildConfig.Load(path, opts));
            });

    private static void ProcessProject(BuildConfig config)
    {
        var project = new StreamerBotProject(config);

        if (project is { BuildSuccess: false, Errors: { Count: { } count } errors })
        {
            Console.WriteLine($"Build failed with {count} compilation errors.\n");

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
