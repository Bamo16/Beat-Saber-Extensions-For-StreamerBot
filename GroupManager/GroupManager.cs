using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Streamer.bot.Common.Events;
using Streamer.bot.Plugin.Interface;
using Streamer.bot.Plugin.Interface.Enums;
using Streamer.bot.Plugin.Interface.Model;

public class CPHInline : CPHInlineBase // Remove " : CPHInlineBase" when pasting code into streamer.bot
{
    private const string ActionName = "Group Manager";
    private const string RefreshLastLiveTimestampTimerId = "fe0b585f-4d61-458d-897b-5d6e876cc574";
    private const string AllKnownUsersGroup = "All Known Users";
    private const string LocalizedDisplayUsersGroup = "Localized DisplayName Users";
    private const string StreamerBotUsersGroup = "StreamerBot Users";
    private const string RaidRequestorGroupName = "Raid Requestors";

    private static readonly object _clearGroupsLock = new();
    private static readonly TimeSpan _timeBetweenStreamsThreshold = TimeSpan.FromMinutes(30);

    private StreamerBotLogger _logger;

    private DateTime LastLiveTimestamp
    {
        get => CPH.GetDateTimeGlobalVar("Obs.LastLiveTimestamp");
        set => CPH.SetDateTimeGlobalVar("Obs.LastLiveTimestamp", value);
    }
    private List<string> GroupsToClearOnNewStream
    {
        get => CPH.GetGlobalVar<List<string>>("GroupManager.GroupsToClearOnNewStream") ?? [];
        set => CPH.SetGlobalVar("GroupManager.GroupsToClearOnNewStream", value);
    }
    private bool ShouldClearGroups =>
        DateTime.UtcNow > LastLiveTimestamp + _timeBetweenStreamsThreshold;

    public void Init()
    {
        try
        {
            _logger = new StreamerBotLogger(CPH, ActionName);
            InitializeGroups();

            _logger.Log("Completed successfully.");
        }
        catch (Exception ex)
        {
            if (_logger is not null)
                _logger.HandleException(ex, setArgument: false);
            else
                CPH.HandleNullLoggerOnInit(ex, nameof(_logger), ActionName);
        }
    }

    public bool Execute()
    {
        _logger.LogActionStart(args, out var executeSuccess, out var sbArgs, out var eventType);

        try
        {
            switch (eventType)
            {
                case EventType.StreamerbotStarted:
                case EventType.ObsStreamingStarted:
                case EventType.TimedAction:
                    SetLastLiveTimestamp();
                    executeSuccess = true;
                    break;

                case EventType.CommandTriggered:
                    executeSuccess = HandleAddUserToGroupsCommand(sbArgs);
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported EventType: {eventType}.");
            }
        }
        catch (Exception ex)
        {
            _logger.HandleException(ex);
        }
        finally
        {
            _logger.LogActionCompletion(executeSuccess);
        }

        return executeSuccess;
    }

    private void SetLastLiveTimestamp()
    {
        if (!CPH.ObsIsConnected() || !CPH.ObsIsStreaming())
        {
            return;
        }

        lock (_clearGroupsLock)
        {
            if (ShouldClearGroups)
            {
                var message =
                    $"{nameof(LastLiveTimestamp)} was longer than {_timeBetweenStreamsThreshold.ToFriendlyString()} ago. Clearing users from the following groups: [{string.Join(", ", GroupsToClearOnNewStream)}].";

                _logger.LogWarn(message);

                foreach (var group in GroupsToClearOnNewStream)
                {
                    CPH.EnsureGroupState(group, true);
                    CPH.ClearUsersFromGroup(group);
                }
            }

            if (!CPH.GetTimerState(RefreshLastLiveTimestampTimerId))
            {
                CPH.EnableTimerById(RefreshLastLiveTimestampTimerId);
            }

            var now = DateTime.UtcNow;

            _logger.Log($"Setting {nameof(LastLiveTimestamp)} to {now} in 60 seconds.");

            LastLiveTimestamp = now;
        }
    }

    private bool HandleAddUserToGroupsCommand(Dictionary<string, object> sbArgs)
    {
        if (!sbArgs.TryGetArg("userId", out string userId))
        {
            _logger.LogError($"Failed to get {nameof(userId)} from arguments.");

            return false;
        }

        if (CPH.TwitchGetUserInfoById(userId) is not { } user)
        {
            _logger.LogError($"Failed to get user info for userId: {userId}.");

            return false;
        }

        if (
            CPH.EnsureGroupMembershipForUser(user, AllKnownUsersGroup, true)
            && (
                !user.HasLocalizedDisplayName()
                || CPH.EnsureGroupMembershipForUser(user, LocalizedDisplayUsersGroup, true)
            )
        )
        {
            return true;
        }

        _logger.LogError($"Failed to add user to groups: {user.GetFormattedDisplayName()}.");
        return false;
    }

    private void InitializeGroups()
    {
        var groupsToClear = GroupsToClearOnNewStream;

        if (!groupsToClear.Contains(RaidRequestorGroupName))
        {
            groupsToClear = [.. groupsToClear, RaidRequestorGroupName];
            GroupsToClearOnNewStream = groupsToClear;
        }

        var ensureGroupsExist = new List<string>(
            [
                .. groupsToClear,
                AllKnownUsersGroup,
                LocalizedDisplayUsersGroup,
                StreamerBotUsersGroup,
            ]
        );

        foreach (var group in ensureGroupsExist)
            CPH.EnsureGroupState(group, true);

        CPH.ClearUsersFromGroup(StreamerBotUsersGroup);

        foreach (var user in CPH.GetStreamerBotAccounts())
            CPH.EnsureGroupMembershipForUser(user, StreamerBotUsersGroup, true);
    }
}

public class StreamerBotLogger(
    IInlineInvokeProxy cph,
    string logMessageTag,
    LogAction defaultLogAction = LogAction.Info,
    int afterChars = 1000,
    int truncateAfterCharsError = 3000
)
{
    private static readonly object _lock = new();

    public void LogDebug(
        string logLine,
        int? truncateAfterChars = null,
        [CallerMemberName] string methodName = null,
        [CallerLineNumber] int lineNumber = 0
    ) => Log(logLine, LogAction.Debug, truncateAfterChars, methodName, lineNumber);

    public void LogVerbose(
        string logLine,
        int? truncateAfterChars = null,
        [CallerMemberName] string methodName = null,
        [CallerLineNumber] int lineNumber = 0
    ) => Log(logLine, LogAction.Verbose, truncateAfterChars, methodName, lineNumber);

    public void LogInfo(
        string logLine,
        int? truncateAfterChars = null,
        [CallerMemberName] string methodName = null,
        [CallerLineNumber] int lineNumber = 0
    ) => Log(logLine, LogAction.Info, truncateAfterChars, methodName, lineNumber);

    public void LogWarn(
        string logLine,
        int? truncateAfterChars = null,
        [CallerMemberName] string methodName = null,
        [CallerLineNumber] int lineNumber = 0
    ) => Log(logLine, LogAction.Warn, truncateAfterChars, methodName, lineNumber);

    public void LogError(
        string logLine,
        int? truncateAfterChars = null,
        [CallerMemberName] string methodName = null,
        [CallerLineNumber] int lineNumber = 0
    ) => Log(logLine, LogAction.Error, truncateAfterChars, methodName, lineNumber);

    public void LogActionStart(
        Dictionary<string, object> args,
        out bool executeSuccess,
        out Dictionary<string, object> sbArgs,
        out EventType eventType,
        [CallerMemberName] string methodName = null,
        [CallerLineNumber] int lineNumber = 0
    )
    {
        lock (_lock)
        {
            executeSuccess = false;
            sbArgs = new Dictionary<string, object>(args);
            eventType = cph.GetEventType();
        }

        Log("Action Started.", methodName: methodName, lineNumber: lineNumber);
    }

    public void LogActionCompletion(
        bool executeSuccess,
        string successArgName = "ExecuteSuccess",
        [CallerMemberName] string methodName = null,
        [CallerLineNumber] int lineNumber = 0
    )
    {
        cph.SetArgument(successArgName, executeSuccess);

        Log(
            $"Action completed with {successArgName}: {executeSuccess}.",
            executeSuccess ? LogAction.Info : LogAction.Warn,
            methodName: methodName,
            lineNumber: lineNumber
        );
    }

    public void HandleException(
        Exception ex,
        bool setArgument = true,
        int? truncateAfterChars = null,
        string argName = "CSharpErrorMessage",
        [CallerMemberName] string methodName = null,
        [CallerLineNumber] int lineNumber = 0
    )
    {
        var message = ex.GetFormattedExceptionMessage();

        if (setArgument)
        {
            var argumentValue = cph.TryGetArg<string>(argName, out var currentValue)
                ? string.Join("; ", currentValue, message)
                : message;

            cph.SetArgument(argName, argumentValue);
        }

        LogError(message, truncateAfterChars, methodName, lineNumber);
    }

    public void Log(
        string logLine,
        LogAction? logAction = null,
        int? truncateAfterChars = null,
        [CallerMemberName] string methodName = null,
        [CallerLineNumber] int lineNumber = 0
    )
    {
        Action<string> action = (logAction ?? defaultLogAction) switch
        {
            _ when cph is null => _ => { },
            LogAction.Debug => cph.LogDebug,
            LogAction.Verbose => cph.LogVerbose,
            LogAction.Info => cph.LogInfo,
            LogAction.Warn => cph.LogWarn,
            LogAction.Error => cph.LogError,
            _ => _ => { },
        };

        action.Invoke(
            Truncate(
                $"[{logMessageTag}] [{methodName} L{lineNumber}] {logLine}",
                logAction,
                truncateAfterChars
            )
        );
    }

    private string Truncate(
        string logLine,
        LogAction? logAction = null,
        int? truncateAfterChars = null
    ) =>
        logLine.Truncate(
            truncateAfterChars
                ?? (logAction is LogAction.Error ? truncateAfterCharsError : afterChars)
        );
}

public static class InlineInvokeProxyExtensions
{
    public static DateTime GetDateTimeGlobalVar(
        this IInlineInvokeProxy cph,
        string varName,
        bool persisted = true,
        DateTime defaultValue = default
    ) =>
        cph.GetGlobalVar<string>(varName, persisted).TryParseDateTime(out var value)
            ? value
            : defaultValue;

    public static void SetDateTimeGlobalVar(
        this IInlineInvokeProxy cph,
        string varName,
        DateTime? value = null,
        bool persisted = true
    ) =>
        cph.SetGlobalVar(
            varName,
            (value ?? DateTime.UtcNow).ToLocalTime().ToString("yyyy-MM-dd hh:mm:ss.fff tt zzz"),
            persisted
        );

    public static IEnumerable<BaseUserInfo> GetStreamerBotAccounts(this IInlineInvokeProxy cph) =>
        (Broadcaster: cph.TwitchGetBroadcaster(), Bot: cph.TwitchGetBot()) switch
        {
            { Broadcaster: { } broadcaster, Bot: { } bot } => [broadcaster, bot],
            { Broadcaster: { } broadcaster } => [broadcaster],
            { Bot: { } bot } => [bot],
            _ => [],
        };

    private static bool TryParseDateTime(this string value, out DateTime result)
    {
        if (!DateTime.TryParse(value, null, DateTimeStyles.RoundtripKind, out result))
            return false;

        if (result is { Kind: DateTimeKind.Unspecified })
            result = DateTime.SpecifyKind(result, DateTimeKind.Local);

        result = result.ToUniversalTime();

        return true;
    }

    public static bool EnsureGroupMembershipForUser(
        this IInlineInvokeProxy cph,
        BaseUserInfo user,
        string groupName,
        bool shouldBelongToGroup
    ) =>
        cph.EnsureGroupState(groupName, true)
        && user is { UserId: { } userId }
        && (
            cph.UserIdInGroup(userId, Platform.Twitch, groupName) == shouldBelongToGroup
            || (
                shouldBelongToGroup
                    ? cph.AddUserIdToGroup(userId, Platform.Twitch, groupName)
                    : cph.RemoveUserIdFromGroup(userId, Platform.Twitch, groupName)
            )
        );

    public static bool EnsureGroupState(
        this IInlineInvokeProxy cph,
        string groupName,
        bool groupExists
    ) =>
        cph.GroupExists(groupName) == groupExists
        || (groupExists ? cph.AddGroup(groupName) : cph.DeleteGroup(groupName));

    public static void HandleNullLoggerOnInit(
        this IInlineInvokeProxy cph,
        Exception ex,
        string loggerName,
        string actionName,
        [CallerMemberName] string callerName = null
    )
    {
        var aggEx = new AggregateException(
            ex,
            new ArgumentNullException(loggerName, $"{loggerName} is null.")
        );

        cph.LogError(
            $"[{actionName}] [{callerName ?? "Unknown Method"}] {aggEx.GetFormattedExceptionMessage()}"
        );
    }
}

public static class BaseUserInfoExtensions
{
    public static string GetFormattedDisplayName(this BaseUserInfo user) =>
        string.Concat(
            $"@{user.UserName}",
            user.HasLocalizedDisplayName() ? $" ({user.UserLogin})" : string.Empty
        );

    public static bool HasLocalizedDisplayName(this BaseUserInfo user) =>
        !string.Equals(user.UserName, user.UserLogin, StringComparison.OrdinalIgnoreCase);
}

public static class DictionaryExtensions
{
    public static T GetArgOrDefault<T>(
        this IDictionary<string, object> sbArgs,
        string argName,
        T defaultValue = default
    ) => sbArgs.TryGetArg(argName, out T value) ? value : defaultValue;

    public static bool TryGetArg<T>(
        this IDictionary<string, object> sbArgs,
        string argName,
        out T value
    )
    {
        // Throw an exception if argName is null or empty
        if (string.IsNullOrEmpty(argName))
        {
            throw new ArgumentNullException(
                nameof(argName),
                $"{nameof(argName)} cannot be null or empty."
            );
        }

        // When key is not present in Dictionary, return false
        if (!sbArgs.TryGetValue(argName, out var untypedValue))
        {
            value = default;
            return false;
        }

        // When T is an enum, attempt to parse as enum
        if (typeof(T) is { IsEnum: true } type)
        {
            try
            {
                value = (T)Enum.Parse(type, untypedValue.ToString(), true);
                return true;
            }
            catch
            {
                value = default;
                return false;
            }
        }

        // Attempt to cast from object to T using pattern matching
        if (untypedValue is T typedValue)
        {
            value = typedValue;
            return value is not null
                && (typeof(T) != typeof(string) || !string.IsNullOrEmpty(value.ToString()));
        }

        // Attempt to convert from object to T
        if (Convert.ChangeType(untypedValue.ToString(), typeof(T)) is T convertedValue)
        {
            value = convertedValue;
            return value is not null
                && (typeof(T) != typeof(string) || !string.IsNullOrEmpty(value.ToString()));
        }

        // Failed to cast/convert
        value = default;
        return false;
    }
}

public static class TimeSpanExtensions
{
    public static string ToFriendlyString(
        this TimeSpan timeSpan,
        TimeUnit precision = TimeUnit.Second
    ) =>
        new (TimeUnit Unit, int Value)[]
        {
            (TimeUnit.Day, (int)timeSpan.TotalDays),
            (TimeUnit.Hour, timeSpan.Hours),
            (TimeUnit.Minute, timeSpan.Minutes),
            (TimeUnit.Second, timeSpan.Seconds),
        }
            .TakeWhile(part => part.Unit >= precision)
            .Where(part => part is { Value: not 0 })
            .Select(part => part.Unit.Pluralize(part.Value))
            .ToList() switch
        {
            { Count: 0 } => precision.Pluralize(0),
            { Count: 1 } parts => parts[0],
            { Count: 2 } parts => $"{parts[0]} and {parts[1]}",
            { Count: var count } parts => string.Concat(
                string.Join(", ", parts.Take(count - 1)),
                ", and ",
                parts.Last()
            ),
        };

    private static string Pluralize<T>(this TimeUnit unit, T value)
        where T : IFormattable => unit.ToString().ToLower().Pluralize(value);
}

public static class StringExtensions
{
    public static string Truncate(
        this string message,
        int maxLength = 500,
        string truncationReplacement = "…"
    ) =>
        (
            Message: new StringInfo((message ?? string.Empty).Trim()),
            Truncation: new StringInfo((truncationReplacement ?? string.Empty).Trim())
        ) switch
        {
            _ when maxLength <= 0 => string.Empty,

            { Truncation: { LengthInTextElements: var truncLength, String: var trunc } }
                when truncLength >= maxLength => trunc,

            {
                Message: { LengthInTextElements: var msgLength } msgInfo,
                Truncation: { LengthInTextElements: var truncLength, String: var trunc }
            } when msgLength > maxLength => string.Concat(
                msgInfo.SubstringByTextElements(0, maxLength - truncLength).Trim(),
                trunc
            ),

            { Message.String: var msg } => msg.Trim(),
        };

    public static string Pluralize<T>(
        this string noun,
        T quantity,
        bool excludeQuantity = false,
        string format = null,
        string customPlural = null
    )
        where T : IFormattable =>
        string.Concat(
            excludeQuantity ? string.Empty : $"{quantity.ToStringWithFormatOrDefault(format)} ",
            quantity.FormattedEquals(1, format) ? noun : customPlural ?? $"{noun}s"
        );
}

public static class FormattableExtensions
{
    public static bool FormattedEquals<T>(
        this T value,
        IFormattable compareValue,
        string format = null
    )
        where T : IFormattable =>
        value.GetFormatOrDefault(format) is var numberFormat
        && value.ToStringWithFormatOrDefault(numberFormat)
            == compareValue.ToStringWithFormatOrDefault(numberFormat);

    public static string ToStringWithFormatOrDefault<T>(this T value, string format)
        where T : IFormattable => value.ToString(value.GetFormatOrDefault(format), null);

    private static string GetFormatOrDefault<T>(this T value, string format = null)
        where T : IFormattable =>
        value switch
        {
            _ when !string.IsNullOrEmpty(format) => format,
            byte or sbyte or short or ushort or int or uint or long or ulong => "N0",
            float or double or decimal => "N3",
            _ => throw new ArgumentException("Value must be a numeric type.", nameof(value)),
        };
}

public static class ExceptionExtensions
{
    private static readonly Regex _dynamicClassPattern = new(
        @"(?<=^ {3}at )\w[a-f0-9]{31}\.",
        RegexOptions.Compiled | RegexOptions.Multiline
    );
    private static readonly Regex _asyncMessageFilterPattern = new(
        @"^ {3}at System\.Runtime\.(?:ExceptionServices\.ExceptionDispatchInfo\.Throw|CompilerServices\.TaskAwaiter)",
        RegexOptions.Compiled
    );

    public static string GetFormattedExceptionMessage(this Exception ex)
    {
        using var writer = new IndentedTextWriter(new StringWriter());

        ex.FormatExceptionDetails(writer);

        return writer.InnerWriter.ToString();
    }

    private static void FormatExceptionDetails(
        this Exception ex,
        IndentedTextWriter writer,
        bool isInner = false
    )
    {
        if (isInner)
        {
            writer.WriteLine("Caused by:");
            writer.Indent++;
        }

        writer.WriteLine(ex.GetType().Name);
        writer.Indent++;

        // Handle multi-line messages by splitting and indenting each line
        var messageLines = ex.Message.Split([Environment.NewLine], StringSplitOptions.None);
        writer.WriteLine("Message:");
        writer.Indent++;

        foreach (var line in messageLines)
            writer.WriteLine(line);

        writer.Indent--;

        switch (ex)
        {
            case AggregateException { InnerExceptions.Count: > 0 } aggEx:
                aggEx.FormatAggregateException(writer);
                break;
            case { InnerException: { } innerEx }:
                innerEx.FormatExceptionDetails(writer, isInner: true);
                break;
        }

        ex.StackTrace?.FormatStackTrace(writer);

        writer.Indent -= isInner ? 2 : 1;
    }

    private static void FormatAggregateException(
        this AggregateException aggEx,
        IndentedTextWriter writer
    )
    {
        if (aggEx is not { InnerExceptions: { Count: > 0 and var count } innerExceptions })
            return;

        writer.WriteLine($"{"Inner Exception".Pluralize(count, excludeQuantity: true)}:");
        writer.Indent++;

        for (var i = 0; i < count; i++)
        {
            writer.WriteLine($"[{i + 1}]");
            innerExceptions[i].FormatExceptionDetails(writer, isInner: true);
        }

        writer.Indent--;
    }

    private static void FormatStackTrace(this string stackTrace, IndentedTextWriter writer)
    {
        if (string.IsNullOrWhiteSpace(stackTrace))
            return;

        var frames = _dynamicClassPattern
            .Replace(stackTrace, string.Empty)
            .Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);

        var filteredFrames = frames
            .Where(frame => !_asyncMessageFilterPattern.IsMatch(frame))
            .ToList();

        if (filteredFrames is not { Count: > 0 })
            return;

        writer.WriteLine("Stack Trace:");

        foreach (var frame in filteredFrames)
            writer.WriteLine(frame);
    }
}

public enum TimeUnit
{
    Second,
    Minute,
    Hour,
    Day,
}

public enum LogAction
{
    Debug,
    Verbose,
    Info,
    Warn,
    Error,
}
