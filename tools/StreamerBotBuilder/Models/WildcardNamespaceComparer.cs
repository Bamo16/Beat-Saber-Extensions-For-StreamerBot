using System.Text.RegularExpressions;

namespace StreamerBotBuilder.Models;

public class WildcardNamespaceComparer(IEnumerable<string> namespacePatterns)
    : IComparer<StreamerBotProjectCsFile>
{
    private readonly List<NamespaceOrderRule> _rules =
    [
        .. namespacePatterns.Select((pattern, index) => new NamespaceOrderRule(pattern, index)),
    ];

    public int Compare(StreamerBotProjectCsFile x, StreamerBotProjectCsFile y) =>
        GetPriority(x.Namespace) is var xPri && GetPriority(y.Namespace) is var yPri && xPri != yPri
            ? xPri.CompareTo(yPri)
            : string.Compare(x.FileInfo.Name, y.FileInfo.Name, StringComparison.OrdinalIgnoreCase);

    private int GetPriority(string namespaceName) =>
        namespaceName is "CPHInline"
            ? -1
            : _rules.FirstOrDefault(rule => rule.IsMatch(namespaceName))?.Priority ?? int.MaxValue;

    private class NamespaceOrderRule(string pattern, int orderPriority)
    {
        private readonly Regex _pattern = CreateRegex(pattern);

        public int Priority { get; } = orderPriority;

        public bool IsMatch(string namespaceName) =>
            !string.IsNullOrWhiteSpace(namespaceName) && _pattern.IsMatch(namespaceName);

        private static Regex CreateRegex(string pattern) =>
            new Regex(
                string.Concat("^", Regex.Escape(pattern).Replace("\\*", ".*"), "$"),
                RegexOptions.Compiled
            );
    }
}
