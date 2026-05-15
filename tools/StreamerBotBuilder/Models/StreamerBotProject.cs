using BuildBeatSaberExtensions.Utility;
using CSharpier.Core;
using CSharpier.Core.CSharp;

namespace BuildBeatSaberExtensions.Models;

public class StreamerBotProject(BuildConfig config)
{
    private readonly List<StreamerBotProjectCsFile> _csFiles = GetOrderedProjectFiles(config);

    private Lazy<string> _unformattedCode;
    private Lazy<CodeFormatterResult> _buildResult;

    private string UnformattedCode =>
        (_unformattedCode ??= new Lazy<string>(GetFullCode)).Value;
    private CodeFormatterResult BuildResult =>
        (
            _buildResult ??= new Lazy<CodeFormatterResult>(() =>
                CSharpFormatter.Format(UnformattedCode)
            )
        ).Value;

    public bool BuildSuccess => !BuildResult.CompilationErrors.Any();
    public string Code => BuildSuccess ? _buildResult.Value.Code : UnformattedCode;
    public int LineCount => Code.Split([Environment.NewLine], StringSplitOptions.None).Length;
    public List<string> Errors =>
        [.. BuildResult.CompilationErrors.Select(error => error.ToString())];

    private string GetFullCode() =>
        string.Join(Environment.NewLine, GetHeader(), GetLogComments(), GetCodeBody());

    private string GetHeader() =>
        string.Join(
            Environment.NewLine,
            _csFiles
                .SelectMany(file => file.Dependencies)
                .Distinct()
                .Select(namespaceName => $"using {namespaceName};")
        );

    private string GetLogComments() =>
        config.IncludeLogCommentsInOutput
            ? string.Join(
                Environment.NewLine,
                [
                    string.Empty,
                    "// ---- Build Info ----",
                    $"// File count: {_csFiles.Count}",
                    $"// Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    .. config.AdditionalLogComments.Select(comment => $"// {comment}"),
                    "// --------------------",
                    string.Empty,
                ]
            )
            : string.Empty;

    private string GetCodeBody() =>
        string.Join(Environment.NewLine, _csFiles.Select(file => file.Code));

    private static List<StreamerBotProjectCsFile> GetOrderedProjectFiles(BuildConfig config) =>
        [
            .. GetFiles(config)
                .Select(fileInfo => new StreamerBotProjectCsFile(
                    fileInfo,
                    config.RootNamespace
                ))
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
