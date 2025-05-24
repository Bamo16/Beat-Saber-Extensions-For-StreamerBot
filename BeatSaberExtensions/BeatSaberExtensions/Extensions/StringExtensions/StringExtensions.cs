using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BeatSaberExtensions.Extensions.FormattableExtensions;

namespace BeatSaberExtensions.Extensions.StringExtensions;

public static class StringExtensions
{
    private const int MaxChatMessageLength = 500;
    private const int MaxChatLineLength = 35;
    private const string TruncationReplacement = "…";
    private const char UnicodeWhitespaceChar = '⠀';

    public static string Pluralize<T>(
        this string noun,
        T quantity,
        bool excludeQuantity = false,
        string format = null,
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
        var sb = new StringBuilder();

        var enumerator = StringInfo.GetTextElementEnumerator(sanitized);

        // Input is longer than maxLength. Truncate and append truncationReplacement.
        while (enumerator.MoveNext() && enumerator.GetTextElement() is var element)
        {
            if (sb.Length + element.Length > targetLength)
            {
                return sb.Append(truncationReplacement).ToString();
            }

            sb.Append(element);
        }

        return sb.Append(truncationReplacement).ToString();
    }

    public static string FormatMultilineChatMessage(
        this IEnumerable<string> chatLines,
        int maxMessageLength = 500,
        string lineTruncationReplacement = TruncationReplacement,
        string messageTruncationReplacement = TruncationReplacement
    ) =>
        string.Join(
                "\n",
                chatLines.Select(line =>
                    line.FormatChatMessageLine(maxMessageLength, lineTruncationReplacement)
                )
            )
            .Truncate(maxMessageLength, messageTruncationReplacement);

    public static bool TryParseDateTime(this string value, out DateTime result)
    {
        if (!DateTime.TryParse(value, null, DateTimeStyles.RoundtripKind, out result))
            return false;

        if (result is { Kind: DateTimeKind.Unspecified })
            result = DateTime.SpecifyKind(result, DateTimeKind.Local);

        result = result.ToUniversalTime();
        return true;
    }

    private static string FormatChatMessageLine(
        this string chatLine,
        int maxLineLength = MaxChatLineLength,
        string truncationReplacement = TruncationReplacement
    ) =>
        string.Join(
                UnicodeWhitespaceChar.ToString(),
                chatLine
                    .Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(part => part.Trim())
            )
            .Truncate(maxLineLength, truncationReplacement)
            .PadRight(maxLineLength, UnicodeWhitespaceChar);
}
