using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using StreamerBotBuilder.Utility;

namespace StreamerBotBuilder.Models;

[method: JsonConstructor]
public class BuildConfig(
    List<string> namespaceOrder,
    List<string> excludeSubDirectories,
    bool copyToClipboard,
    List<string> additionalLogComments,
    string outputFileName,
    List<string> csprojSubstitutions
)
{
    private const string BuildConfigFileName = "BuildConfig.json";

    private static readonly JsonSerializerOptions _jsonSerializerOptions =
        new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };

    public string ProjectRootPath { get; private set; }
    public string RootNamespace { get; private set; }
    public string OutputFileName { get; private set; } = outputFileName;
    public bool CopyToClipboard { get; private set; } = copyToClipboard;
    public Dictionary<string, string> SubstitutionValues { get; private set; } = [];

    public List<string> ExcludeSubDirectories { get; } =
        GetExcludeSubDirectories(excludeSubDirectories);
    public List<string> NamespaceOrder { get; } = namespaceOrder ?? [];
    public List<string> AdditionalLogComments { get; } = additionalLogComments ?? [];
    public List<string> CsprojSubstitutions { get; } = csprojSubstitutions ?? [];

    public static BuildConfig Load(string projectRootPath, CommandLineOptions opts)
    {
        var configFile = Path.Combine(projectRootPath, BuildConfigFileName);

        BuildConfig config = File.Exists(configFile)
            ? JsonSerializer.Deserialize<BuildConfig>(
                File.ReadAllText(configFile),
                _jsonSerializerOptions
            ) ?? throw new InvalidDataException($"Failed to deserialize {BuildConfigFileName}.")
            : new BuildConfig(null, null, false, null, null, null);

        config.ProjectRootPath = Path.GetFullPath(projectRootPath);

        var (rootNamespace, substitutionValues) = ReadCsprojValues(
            projectRootPath,
            config.CsprojSubstitutions
        );
        config.RootNamespace = rootNamespace;
        config.SubstitutionValues = substitutionValues;

        if (config.OutputFileName is { } outputFileName)
            config.OutputFileName = PathUtility.ResolvePath(projectRootPath, outputFileName);

        if (opts.CopyToClipboard)
            config.CopyToClipboard = true;

        if (opts.OutputFile is { } outputFile)
            config.OutputFileName = PathUtility.ResolvePath(projectRootPath, outputFile);

        return config;
    }

    private static (
        string RootNamespace,
        Dictionary<string, string> SubstitutionValues
    ) ReadCsprojValues(string projectRootPath, List<string> substitutionKeys)
    {
        var projectRoot = new DirectoryInfo(projectRootPath);
        var projectFilePath = FindProjectFile(projectRoot);
        var doc = XDocument.Load(projectFilePath);
        var msbuild = doc.Root.Name.Namespace;

        var rootNamespace = doc.Descendants(msbuild + "RootNamespace")
            .Select(el => el.Value)
            .DefaultIfEmpty(projectRoot.Name)
            .First();

        var substitutionValues = substitutionKeys
            .Select(key => (key, value: doc.Descendants(msbuild + key).FirstOrDefault()?.Value))
            .Where(pair => pair.value is not null)
            .ToDictionary(pair => pair.key, pair => pair.value);

        return (rootNamespace, substitutionValues);
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
