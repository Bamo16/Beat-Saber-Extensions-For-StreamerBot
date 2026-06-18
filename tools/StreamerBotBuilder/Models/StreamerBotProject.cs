using CSharpier.Core;
using CSharpier.Core.CSharp;
using StreamerBotBuilder.Utility;

namespace StreamerBotBuilder.Models;

public class StreamerBotProject(BuildConfig config)
{
    private readonly List<StreamerBotProjectCsFile> _csFiles = GetOrderedProjectFiles(config);

    private Lazy<string> _unformattedCode;
    private Lazy<string> _tokenizedCode;
    private Lazy<CodeFormatterResult> _buildResult;

    private string UnformattedCode =>
        (_unformattedCode ??= new Lazy<string>(GetUnformattedCode)).Value;
    private string TokenizedCode =>
        (_tokenizedCode ??= new Lazy<string>(() => ApplyTokenReplacements(UnformattedCode))).Value;
    private CodeFormatterResult BuildResult =>
        (
            _buildResult ??= new Lazy<CodeFormatterResult>(() =>
                CSharpFormatter.Format(TokenizedCode)
            )
        ).Value;

    public bool BuildSuccess => !BuildResult.CompilationErrors.Any();
    public int LineCount => Code.Split([Environment.NewLine], StringSplitOptions.None).Length;
    public List<string> Errors =>
        [.. BuildResult.CompilationErrors.Select(error => error.ToString())];

    public string Code => BuildSuccess ? BuildResult.Code : TokenizedCode;

    private string GetUnformattedCode() =>
        string.Join(Environment.NewLine, GetBuildHeader(), GetUsingStatements(), GetCodeBody());

    private string GetUsingStatements() =>
        string.Join(
            Environment.NewLine,
            _csFiles
                .SelectMany(file => file.Dependencies)
                .Distinct()
                .Select(namespaceName => $"using {namespaceName};")
        );

    private string GetCodeBody() =>
        string.Join(Environment.NewLine, _csFiles.Select(file => file.Code));

    private string GetBuildHeader() =>
        string.Join(
            Environment.NewLine,
            [
                string.Empty,
                "// =====================================================",
                "// AUTO-GENERATED — Do not edit directly.",
                $"// Merged from {_csFiles.Count} source files by StreamerBotBuilder.",
                "// To make changes, edit the source files and rebuild.",
                "// =====================================================",
                .. config.AdditionalLogComments.Select(comment => $"// {comment}"),
                $"// Built: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                "// =====================================================",
                string.Empty,
            ]
        );

    private string ApplyTokenReplacements(string code) =>
        config.SubstitutionValues.Aggregate(
            code,
            (current, kvp) => current.Replace($"{{{kvp.Key}}}", kvp.Value)
        );

    private static List<StreamerBotProjectCsFile> GetOrderedProjectFiles(BuildConfig config) =>
        [
            .. GetFiles(config)
                .Select(fileInfo => new StreamerBotProjectCsFile(fileInfo, config.RootNamespace))
                .OrderBy(file => file, new WildcardNamespaceComparer(config.NamespaceOrder)),
        ];

    private static IEnumerable<FileInfo> GetFiles(BuildConfig config) =>
        Directory
            .GetFiles(config.ProjectRootPath, "*.cs", SearchOption.AllDirectories)
            .Where(filePath =>
                (config is not { OutputFileName: { } output } || filePath != output)
                && !PathUtility
                    .GetRelativePath(config.ProjectRootPath, filePath)
                    .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Any(config.ExcludeSubDirectories.Contains)
            )
            .Select(filePath => new FileInfo(filePath));
}
