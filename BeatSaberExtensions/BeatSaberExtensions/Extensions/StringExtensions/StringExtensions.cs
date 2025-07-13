using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BeatSaberExtensions.Extensions.FormattableExtensions;

namespace BeatSaberExtensions.Extensions.StringExtensions;

public static class StringExtensions
{
    private const int MaxChatMessageLength = 500;
    private const int MaxChatLineLength = 35;
    private const string TruncationReplacement = "…";
    private const char BraillePatternBlankChar = '\u2800';

    private static readonly Regex _whitespacePattern = new Regex(@"\s+", RegexOptions.Compiled);
    private static readonly Regex _requestPattern = new Regex(
        @"^!bsr\s+(?<BsrId>[a-z0-9]{1,6})\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture
    );

    public static string MatchBsrRequest(this string input) =>
        !string.IsNullOrEmpty(input)
        && _requestPattern.Match(input).Groups["BsrId"] is { Success: true, Value: { } value }
            ? value
            : null;

    public static string Pluralize<T>(
        this string noun,
        T quantity,
        string format = null,
        bool excludeQuantity = false,
        string customPlural = null
    )
        where T : IFormattable
    {
        var nounPart = quantity.Equals(1, format) ? noun : customPlural ?? $"{noun}s";

        if (excludeQuantity)
        {
            return nounPart;
        }

        return $"{quantity.ToString(format, null)} {nounPart}";
    }

    public static bool ContainsNonAscii(this string value) => value.Any(c => c > sbyte.MaxValue);

    public static string Truncate(
        this string input,
        int maxLength = MaxChatMessageLength,
        string truncationReplacement = TruncationReplacement
    )
    {
        var sanitized = (input ?? string.Empty).Trim();

        if (string.IsNullOrEmpty(sanitized) || maxLength <= 0)
        {
            // Input is empty. Return an empty string.
            return string.Empty;
        }

        if (sanitized.Length <= maxLength)
        {
            // Input is shorter than maxLength. Return input as-is.
            return sanitized;
        }

        var trunc = (truncationReplacement ?? string.Empty).Trim();
        var targetLength = maxLength - trunc.Length;
        var builder = new StringBuilder();
        var enumerator = StringInfo.GetTextElementEnumerator(sanitized);

        // Input is longer than maxLength. Truncate and append truncationReplacement.
        while (enumerator.MoveNext() && enumerator.GetTextElement() is var element)
        {
            if (builder.Length + element.Length > targetLength)
            {
                return builder.Append(trunc).ToString();
            }

            builder.Append(element);
        }

        return builder.Append(trunc).ToString();
    }

    public static string FormatMultilineChatMessage(
        this IEnumerable<string> chatLines,
        string header = null,
        string lineTruncationReplacement = TruncationReplacement,
        string messageTruncationReplacement = TruncationReplacement,
        char separatorChar = BraillePatternBlankChar,
        int maxLineLength = MaxChatLineLength
    ) =>
        string.Join(
                "\n",
                (header is null ? chatLines : chatLines.Prepend(header)).Select(line =>
                    line.FormatChatMessageLine(
                        lineTruncationReplacement,
                        separatorChar,
                        maxLineLength
                    )
                )
            )
            .Truncate(MaxChatMessageLength, messageTruncationReplacement);

    public static bool TryParseDateTime(this string value, out DateTime result)
    {
        if (!DateTime.TryParse(value, null, DateTimeStyles.RoundtripKind, out result))
        {
            return false;
        }

        if (result is { Kind: DateTimeKind.Unspecified })
        {
            result = DateTime.SpecifyKind(result, DateTimeKind.Local);
        }

        result = result.ToUniversalTime();
        return true;
    }

    private static string FormatChatMessageLine(
        this string chatLine,
        string truncationReplacement = TruncationReplacement,
        char separatorChar = BraillePatternBlankChar,
        int maxLineLength = MaxChatLineLength
    ) =>
        string.Join(
                separatorChar.ToString(),
                _whitespacePattern.Split(chatLine).Where(part => !string.IsNullOrEmpty(part))
            )
            .Truncate(maxLineLength, truncationReplacement)
            .PadRight(maxLineLength, separatorChar);
}
