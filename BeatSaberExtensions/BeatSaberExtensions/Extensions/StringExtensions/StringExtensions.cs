using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BeatSaberExtensions.Enums;
using BeatSaberExtensions.Extensions.FormattableExtensions;
using BeatSaberExtensions.Extensions.MatchExtensions;
using BeatSaberExtensions.Utility.Arguments;

namespace BeatSaberExtensions.Extensions.StringExtensions;

public static class StringExtensions
{
    private const int MaxChatMessageLength = 500;
    private const int MaxWhisperLineLength = 32;
    private const int MaxChatLineLength = 35;
    private const string TruncationReplacement = "…";
    private const char BraillePatternBlankChar = '\u2800';

    private static readonly string[] _dateTimeFormats =
    [
        "yyyy-MM-dd HH:mm:ss.fff tt zzz",
        "dd.MM.yyyy HH:mm:ss",
        "dd/MM/yyyy HH:mm:ss",
        "yyyy/MM/dd HH:mm:ss",
    ];
    private static readonly Regex _dateTimePattern = new Regex(
        @"
        (?<UnixTimestamp>(?<=\()\d+(?=\)))
        |
        (?<LegacyTimestamp>
            // yyyy-MM-dd HH:mm:ss.fff tt zzz
            \d{4}-\d{2}-\d{2}\s\d{2}:\d{2}:\d{2}\.\d{3}\s(?:AM|PM)\s[-+]\d{2}:\d{2}
            |
            // dd.MM.yyyy HH:mm:ss
            \d{2}\.\d{2}\.\d{4}\s\d{2}:\d{2}:\d{2}
            |
            // dd/MM/yyyy HH:mm:ss
            \d{2}/\d{2}/\d{4}\s\d{2}:\d{2}:\d{2}
            |
            // yyyy/MM/dd HH:mm:ss
            \d{4}/\d{2}/\d{2}\s\d{2}:\d{2}:\d{2}
        )
        ",
        RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture
    );
    private static readonly Regex _whitespacePattern = new Regex(@"\s+", RegexOptions.Compiled);
    private static readonly Regex _requestPattern = new Regex(
        @"^(?:(?<Command>!bsr)(?:\s+))?(?<BsrId>[a-z0-9]{1,6})\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture
    );

    public static string MatchBsrRequest(this string input, bool bsrIdOnly = false) =>
        !string.IsNullOrEmpty(input)
        && _requestPattern.Match(input) is { Success: true, Groups: { } groups }
        && (bsrIdOnly || groups["Command"] is { Success: true })
        && groups["BsrId"] is { Success: true, Value: { } value }
            ? value.ToLowerInvariant()
            : null;

    public static string GetBeatSaverLink(this string bsrId) =>
        $"https://beatsaver.com/maps/{bsrId}";

    public static string Pluralize<T>(
        this string noun,
        T quantity,
        string format = null,
        bool excludeQuantity = false,
        bool toLower = false,
        string customPlural = null
    )
        where T : IFormattable
    {
        var nounPart = quantity.Equals(1, format) ? noun : customPlural ?? $"{noun}s";
        var result = excludeQuantity ? nounPart : $"{quantity.ToString(format, null)} {nounPart}";

        return toLower ? result.ToLowerInvariant() : result;
    }

    public static DateTime FromStreamerBotDateString(
        this string value,
        DateTime? defaultValue = null,
        IFormatProvider provider = null,
        DateTimeStyles styles = DateTimeStyles.AssumeLocal
    ) =>
        _dateTimePattern
            .Match(value ?? string.Empty)
            .GetNamedCaptureGroups()
            .FirstOrDefault() switch
        {
            { Name: "UnixTimestamp", Value: { } v } when long.TryParse(v, out var ms) =>
                DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime,

            { Value: { } v }
                when DateTime.TryParseExact(
                    v,
                    _dateTimeFormats,
                    provider ?? CultureInfo.InvariantCulture,
                    styles,
                    out var dt
                ) => dt.ToUniversalTime(),

            _ => default(DateTime?),
        }
        ?? defaultValue
        ?? DateTime.MinValue;

    public static string SanitizeLookupString(this string lookupString) =>
        (lookupString ?? string.Empty).Trim().TrimStart('@').Trim();

    public static bool ContainsNonAscii(this string value) => value.Any(c => c > sbyte.MaxValue);

    public static string Truncate(
        this string input,
        int maxLength,
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
        ActionContext context,
        string header = null,
        string lineTruncationReplacement = TruncationReplacement,
        string messageTruncationReplacement = TruncationReplacement,
        char separatorChar = BraillePatternBlankChar
    ) =>
        string.Join(
                "\n",
                (header is null ? chatLines : chatLines.Prepend(header)).Select(line =>
                    line.FormatChatMessageLine(
                        lineTruncationReplacement,
                        separatorChar,
                        context is { CommandType: CommandType.BotWhisper } isWhisper
                            ? MaxWhisperLineLength
                            : MaxChatLineLength
                    )
                )
            )
            .Truncate(MaxChatMessageLength, messageTruncationReplacement);

    public static string ReplaceVerticalWhitespace(this string input, string separator = " ") =>
        string.Join(separator, input.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries));

    public static bool CaseInsensitiveContains(this string source, string toCheck) =>
        source.Contains(toCheck, StringComparison.OrdinalIgnoreCase);

    public static string TakeUntilWhitespace(this string source)
    {
        for (var i = 0; i < source.Length; i++)
        {
            if (char.IsWhiteSpace(source[i]))
                return source.Substring(0, i);
        }

        return source;
    }

    private static bool Contains(
        this string source,
        string toCheck,
        StringComparison comparisonType
    ) => source?.IndexOf(toCheck, comparisonType) is >= 0;

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
