using System.Text.RegularExpressions;

namespace StreamerBotBuilder.Models;

public class StreamerBotProjectCsFile
{
    private const string CphClassDeclaration = "public class CPHInline\n";
    private const string NullableDirective = "#nullable enable";
    private const string NullableRestore = "#nullable restore";

    private static readonly Regex _cphOrNamespacePattern = new Regex(
        @"^\s*(?:namespace\s+(?<Namespace>\w+(\.\w+)*?)\s*;|public\s+class\s+CPHInline)",
        RegexOptions.Compiled
    );
    private static readonly Regex _usingStatementPattern = new Regex(
        @"^\s*using\s+(?<Namespace>\w+(?:\.\w+)*);",
        RegexOptions.Compiled
    );
    private static readonly HashSet<string> _excludeNamespaces =
    [
        "Streamer.bot.Plugin.Interface",
        "Streamer.bot.Plugin.Interface.Enums",
        "Streamer.bot.Plugin.Interface.Model",
    ];

    public FileInfo FileInfo { get; }
    public string Namespace { get; }
    public List<string> Dependencies { get; }
    public string Code { get; }
    public bool UsesNullable { get; }

    public StreamerBotProjectCsFile(FileInfo fileInfo, string rootNamespace)
    {
        var lines = File.ReadAllLines(fileInfo.FullName);
        var splitIndex = FindSplitIndex(lines, out var namespaceName);

        if (splitIndex is -1)
        {
            throw new InvalidOperationException(
                $"Failed to parse {fileInfo.FullName}: no namespace declaration or CPHInline class declaration found."
            );
        }

        FileInfo = fileInfo;
        Namespace = namespaceName;
        Dependencies = [.. GetDependencyNames(lines.Take(splitIndex), rootNamespace)];
        UsesNullable = lines.Any(l =>
            l.TrimStart().StartsWith(NullableDirective, StringComparison.Ordinal)
        );

        var body = string.Join(Environment.NewLine, lines.Skip(splitIndex + 1));

        Code = string.Concat(
            [
                namespaceName is "CPHInline" ? CphClassDeclaration : string.Empty,
                body,
                UsesNullable ? $"{Environment.NewLine}{NullableRestore}" : string.Empty,
            ]
        );
    }

    private static int FindSplitIndex(IEnumerable<string> lines, out string @namespace)
    {
        foreach (var (line, index) in lines.Select((line, index) => (line, index)))
        {
            if (MatchCphOrNamespace(line) is { } nsName)
            {
                @namespace = nsName;
                return index;
            }
        }

        @namespace = null;
        return -1;
    }

    private static string MatchCphOrNamespace(string line) =>
        _cphOrNamespacePattern.Match(line) is { Success: true, Groups: { } groups }
            ? groups["Namespace"] is { Success: true, Value: { } value }
                ? value
                : "CPHInline"
            : null;

    private static IEnumerable<string> GetDependencyNames(
        IEnumerable<string> lines,
        string rootNamespace
    ) =>
        lines
            .Select(line => MatchUsingStatement(line, rootNamespace))
            .Where(namespaceName => namespaceName is not null);

    private static string MatchUsingStatement(string line, string rootNamespace) =>
        _usingStatementPattern.Match(line) is { Success: true, Groups: { } groups }
        && groups["Namespace"] is { Value: var value }
        && !_excludeNamespaces.Contains(value)
        && !value.StartsWith(rootNamespace)
            ? value
            : null;
}
