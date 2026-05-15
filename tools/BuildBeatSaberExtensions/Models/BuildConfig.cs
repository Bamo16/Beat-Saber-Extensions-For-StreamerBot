using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using BuildBeatSaberExtensions.Utility;

namespace BuildBeatSaberExtensions.Models;

[method: JsonConstructor]
public class BuildConfig(
    List<string> namespaceOrder,
    List<string> excludeSubDirectories,
    bool copyToClipboard,
    bool includeLogCommentsInOutput,
    List<string> additionalLogComments,
    string outputFileName
)
{
    private const string BuildConfigPath = "BuildConfig.json";

    private static readonly JsonSerializerOptions _jsonSerializerOptions =
        new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };

    public string ProjectRootPath { get; private set; }
    public string RootNamespace { get; private set; }
    public string OutputFileName { get; private set; } = outputFileName;

    public List<string> ExcludeSubDirectories { get; } =
        GetExcludeSubDirectories(excludeSubDirectories);
    public List<string> NamespaceOrder { get; } = namespaceOrder ?? [];
    public bool CopyToClipboard { get; } = copyToClipboard;
    public bool IncludeLogCommentsInOutput { get; } = includeLogCommentsInOutput;
    public List<string> AdditionalLogComments { get; } = additionalLogComments ?? [];

    public static BuildConfig LoadBuildConfig(string projectRootPath)
    {
        var configFile = Path.Combine(projectRootPath, BuildConfigPath);

        if (!File.Exists(configFile))
        {
            throw new FileNotFoundException($"{BuildConfigPath} not found.", configFile);
        }

        var configContent = File.ReadAllText(configFile);
        var config =
            JsonSerializer.Deserialize<BuildConfig>(configContent, _jsonSerializerOptions)
            ?? throw new InvalidDataException("Failed to deserialize BuildConfig.");
        config.ProjectRootPath = projectRootPath;
        config.RootNamespace = FindRootNamespace(projectRootPath);
        config.OutputFileName = config is { OutputFileName: { } output }
            ? PathUtility.ResolvePath(projectRootPath, output)
            : null;

        return config;
    }

    private static string FindRootNamespace(string projectRootPath)
    {
        var projectRoot = new DirectoryInfo(projectRootPath);
        var projectFilePath = FindProjectFile(projectRoot);
        var doc = XDocument.Load(projectFilePath);
        var msbuild = doc.Root.Name.Namespace;

        return doc.Descendants(msbuild + "RootNamespace")
            .Select(prop => prop.Value)
            .DefaultIfEmpty(projectRoot.Name)
            .FirstOrDefault();
    }

    private static string FindProjectFile(DirectoryInfo projectRoot) =>
        (
            TrySearchProjectFile(projectRoot.FullName, out var csproj)
            || TrySearchProjectFile(projectRoot.FullName, out csproj, SearchOption.AllDirectories)
        )
            ? csproj
            : throw new FileNotFoundException(
                $"No .csproj file found in or under '{projectRoot.FullName}'."
            );

    private static bool TrySearchProjectFile(
        string path,
        out string projectFilePath,
        SearchOption searchOption = SearchOption.TopDirectoryOnly
    )
    {
        projectFilePath = Directory.GetFiles(path, "*.csproj", searchOption).FirstOrDefault();

        return projectFilePath is not null;
    }

    private static List<string> GetExcludeSubDirectories(List<string> excludeSubDirectories) =>
        [.. (excludeSubDirectories ?? []).Concat(["bin", "obj"]).Distinct()];
}
