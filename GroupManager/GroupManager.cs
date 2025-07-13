using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Messaging;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Streamer.bot.Common.Events;
using Streamer.bot.Plugin.Interface;
using Streamer.bot.Plugin.Interface.Enums;
using Streamer.bot.Plugin.Interface.Model;

public class CPHInline : CPHInlineBase // Remove " : CPHInlineBase" when pasting code into Streamer.Bot
{
    private const string ActionName = "Group Manager";
    private const string RefreshLastLiveTimestampTimerId = "fe0b585f-4d61-458d-897b-5d6e876cc574";
    private const string AllKnownUsersGroup = "All Known Users";
    private const string LocalizedDisplayUsersGroup = "Localized DisplayName Users";
    private const string StreamerBotUsersGroup = "StreamerBot Users";
    private const string RaidRequestorGroupName = "Raid Requestors";

    private static readonly Predicate<ActionContext> _noOpPredicate = (context) =>
        context
            is {
                EventType: EventType.CommandTriggered,
                Caller.UserId: { } caller,
                Broadcaster.UserId: { } broadcaster
            }
        && caller == broadcaster;
    private static readonly string[] _ensureGroupsExist =
    [
        RaidRequestorGroupName,
        AllKnownUsersGroup,
        LocalizedDisplayUsersGroup,
        StreamerBotUsersGroup,
    ];
    private static readonly object _clearGroupsLock = new object();
    private static readonly TimeSpan _timeBetweenStreamsThreshold = TimeSpan.FromMinutes(30);

    private DateTime LastLiveTimestamp
    {
        get => CPH.GetDateTimeGlobalVar("GroupManager.LastLiveTimestamp");
        set => CPH.SetDateTimeGlobalVar("GroupManager.LastLiveTimestamp", value);
    }
    private List<string> GroupsToClearOnNewStream =>
        CPH.GetCustomGlobalVar<List<string>>(
            "GroupManager.GroupsToClearOnNewStream",
            defaultValue: [RaidRequestorGroupName]
        );
    private bool ShouldClearGroups =>
        DateTime.UtcNow > LastLiveTimestamp + _timeBetweenStreamsThreshold;

    public void Init()
    {
        try
        {
            Logger.Init(CPH, ActionName, _noOpPredicate);
            InitializeGroups();

            Logger.Log("Completed successfully.");
        }
        catch (Exception ex)
        {
            Logger.HandleException(ex, setArgument: false);
        }
    }

    public bool Execute()
    {
        var context = Logger.CreateActionContext(args, out var executeSuccess);

        if (executeSuccess)
            return executeSuccess;

        try
        {
            executeSuccess = context.EventType switch
            {
                EventType.StreamerbotStarted
                or EventType.ObsStreamingStarted
                or EventType.TimedAction => SetLastLiveTimestamp(),

                EventType.CommandTriggered => HandleAddUserToGroupsCommand(context),

                var eventType => throw new InvalidOperationException(
                    $"Unsupported EventType: {eventType}."
                ),
            };
        }
        catch (Exception ex)
        {
            Logger.HandleException(ex);
        }
        finally
        {
            Logger.LogActionCompletion(executeSuccess);
        }

        return executeSuccess;
    }

    private bool SetLastLiveTimestamp()
    {
        if (!CPH.StreamIsLive())
        {
            return true;
        }

        lock (_clearGroupsLock)
        {
            if (ShouldClearGroups)
            {
                var groupsToClear = GroupsToClearOnNewStream;
                var message =
                    $"{nameof(LastLiveTimestamp)} was longer than {_timeBetweenStreamsThreshold.ToFriendlyString()} ago. Clearing users from the following groups: [{string.Join(", ", groupsToClear)}].";

                Logger.LogWarn(message);

                foreach (var group in groupsToClear)
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

            Logger.Log($"Setting {nameof(LastLiveTimestamp)} to {now} in 60 seconds.");

            LastLiveTimestamp = now;
        }

        return true;
    }

    private bool HandleAddUserToGroupsCommand(ActionContext context) =>
        CPH.EnsureGroupMembershipForUser(context.Caller, AllKnownUsersGroup, true)
        && (
            !context.Caller.HasLocalizedDisplayName()
            || CPH.EnsureGroupMembershipForUser(context.Caller, LocalizedDisplayUsersGroup, true)
        );

    private void InitializeGroups()
    {
        foreach (var group in _ensureGroupsExist)
        {
            CPH.EnsureGroupState(group, true);
        }

        CPH.ClearUsersFromGroup(StreamerBotUsersGroup);

        foreach (var user in CPH.GetStreamerBotAccounts())
        {
            CPH.EnsureGroupMembershipForUser(user, StreamerBotUsersGroup, true);
        }
    }
}

public static class Logger
{
    private static IInlineInvokeProxy _cph;
    private static string _logMessageTag;
    private static Predicate<ActionContext> _noOpPredicate;
    private static LogAction _defaultLogAction;
    private static int _truncateAfterChars;
    private static int _truncateAfterCharsError;

    public static bool IsInitialized => _cph is not null;

    public static void Init(
        IInlineInvokeProxy cph,
        string logMessageTag,
        Predicate<ActionContext> noOpPredicate = null,
        LogAction defaultLogAction = LogAction.Info,
        int truncateAfterChars = 1000,
        int truncateAfterCharsError = 3000
    )
    {
        _cph = cph;
        _logMessageTag = logMessageTag;
        _noOpPredicate = noOpPredicate ?? ((context) => true);
        _defaultLogAction = defaultLogAction;
        _truncateAfterChars = truncateAfterChars;
        _truncateAfterCharsError = truncateAfterCharsError;
    }

    public static void LogDebug(
        string logLine,
        int? truncateAfterChars = null,
        [CallerMemberName] string methodName = null,
        [CallerLineNumber] int lineNumber = 0
    ) =>
        Log(
            logLine: logLine,
            logAction: LogAction.Debug,
            truncateAfterChars: truncateAfterChars,
            methodName: methodName,
            lineNumber
        );

    public static void LogVerbose(
        string logLine,
        int? truncateAfterChars = null,
        [CallerMemberName] string methodName = null,
        [CallerLineNumber] int lineNumber = 0
    ) => Log(logLine, LogAction.Verbose, truncateAfterChars, methodName, lineNumber);

    public static void LogInfo(
        string logLine,
        int? truncateAfterChars = null,
        [CallerMemberName] string methodName = null,
        [CallerLineNumber] int lineNumber = 0
    ) => Log(logLine, LogAction.Info, truncateAfterChars, methodName, lineNumber);

    public static void LogWarn(
        string logLine,
        int? truncateAfterChars = null,
        [CallerMemberName] string methodName = null,
        [CallerLineNumber] int lineNumber = 0
    ) => Log(logLine, LogAction.Warn, truncateAfterChars, methodName, lineNumber);

    public static void LogError(
        string logLine,
        int? truncateAfterChars = null,
        [CallerMemberName] string methodName = null,
        [CallerLineNumber] int lineNumber = 0
    ) => Log(logLine, LogAction.Error, truncateAfterChars, methodName, lineNumber);

    public static ActionContext CreateActionContext(
        Dictionary<string, object> args,
        out bool executeSuccess,
        string label = "Action started with",
        [CallerMemberName] string methodName = null,
        [CallerLineNumber] int lineNumber = 0
    )
    {
        if (!IsInitialized)
        {
            throw new InvalidOperationException(
                $"You must call Init() before calling CreateActionContext."
            );
        }

        var context = new ActionContext(args, _cph);

        if (_noOpPredicate.Invoke(context))
        {
            executeSuccess = true;
            SetExecuteSuccessArgument(executeSuccess);

            return context;
        }

        executeSuccess = false;

        context.LogArgs(label, methodName, lineNumber);

        return context;
    }

    public static void LogActionCompletion(
        bool executeSuccess,
        [CallerMemberName] string methodName = null,
        [CallerLineNumber] int lineNumber = 0
    )
    {
        SetExecuteSuccessArgument(executeSuccess);

        Log(
            $"Action completed with ExecuteSuccess: {executeSuccess}.",
            executeSuccess ? LogAction.Info : LogAction.Warn,
            methodName: methodName,
            lineNumber: lineNumber
        );
    }

    public static void LogObject(
        object logObject,
        string label = null,
        LogAction? logAction = null,
        int? truncateAfterChars = null,
        [CallerMemberName] string methodName = null,
        [CallerLineNumber] int lineNumber = 0
    ) =>
        Log(
            JsonConvert.SerializeObject(logObject, Formatting.Indented) is var serialized
            && !string.IsNullOrEmpty(label)
                ? $"{label}:\n{serialized}"
                : serialized,
            logAction,
            truncateAfterChars,
            methodName,
            lineNumber
        );

    public static void HandleException(
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
            var argumentValue = _cph.TryGetArg<string>(argName, out var currentValue)
                ? string.Join("; ", currentValue, message)
                : message;

            _cph.SetArgument(argName, argumentValue);
        }

        LogError(message, truncateAfterChars, methodName, lineNumber);
    }

    public static void Log(
        string logLine,
        LogAction? logAction = null,
        int? truncateAfterChars = null,
        [CallerMemberName] string methodName = null,
        [CallerLineNumber] int lineNumber = 0
    ) =>
        (
            (logAction ?? _defaultLogAction) switch
            {
                _ when _cph is null => _ => { },
                LogAction.Debug => _cph.LogDebug,
                LogAction.Verbose => _cph.LogVerbose,
                LogAction.Info => _cph.LogInfo,
                LogAction.Warn => _cph.LogWarn,
                LogAction.Error => _cph.LogError,
                _ => new Action<string>(_ => { }),
            }
        ).Invoke(
            Truncate(
                $"[{_logMessageTag}] [{methodName} L{lineNumber}] {logLine}",
                logAction,
                truncateAfterChars
            )
        );

    private static string Truncate(
        string logLine,
        LogAction? logAction = null,
        int? truncateAfterChars = null
    ) =>
        logLine.Truncate(
            truncateAfterChars
                ?? (logAction is LogAction.Error ? _truncateAfterCharsError : _truncateAfterChars)
        );

    private static void SetExecuteSuccessArgument(bool executeSuccess) =>
        _cph.SetArgument("ExecuteSuccess", executeSuccess);
}

public class ActionContext(Dictionary<string, object> args, IInlineInvokeProxy cph)
{
    private Lazy<TwitchUserInfo> _caller;

    public Dictionary<string, object> SbArgs { get; } = new Dictionary<string, object>(args);
    public EventType EventType { get; } = cph.GetEventType();

    public TwitchUserInfo Broadcaster => cph.GetBroadcaster();
    public TwitchUserInfo Caller => (_caller ??= new(() => GetUserFromArg<TwitchUserInfo>())).Value;

    public T GetUserFromArg<T>(string argName = "user", T defaultValue = null)
        where T : BaseUserInfo => SbArgs.GetUserFromArg(cph, argName, defaultValue);

    public T Get<T>(string key, T defaultValue = default) => SbArgs.Get(key, defaultValue);

    public bool TryGet<T>(string key, out T value) => SbArgs.TryGet(key, out value);

    public T GetUser<T>(string userLogin)
        where T : BaseUserInfo => cph.GetUser<T>(userLogin);

    public void LogArgs(
        string label = "Action started with",
        [CallerMemberName] string methodName = null,
        [CallerLineNumber] int lineNumber = 0
    ) =>
        Logger.LogObject(
            new { EventType = EventType.ToString(), Caller = Caller?.UserLogin ?? "<null>" },
            label,
            methodName: methodName,
            lineNumber: lineNumber
        );
}

public static class InlineInvokeProxyExtensions
{
    private const string DateTimeFormat = "yyyy-MM-dd hh:mm:ss.fff tt zzz";

    private static TwitchUserInfo _broadcaster;

    public static T GetUser<T>(this IInlineInvokeProxy cph, string lookupString)
        where T : BaseUserInfo =>
        !string.IsNullOrWhiteSpace(lookupString)
            ? (
                typeof(T) == typeof(TwitchUserInfoEx)
                    ? cph.TwitchGetExtendedUserInfoByLogin(lookupString)
                    : cph.TwitchGetUserInfoByLogin(lookupString)
            ) as T
            : null;

    public static TwitchUserInfo GetBroadcaster(this IInlineInvokeProxy cph) =>
        _broadcaster ??= cph.TwitchGetBroadcaster();

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
            (value ?? DateTime.UtcNow).ToLocalTime().ToString(DateTimeFormat),
            persisted
        );

    public static T GetCustomGlobalVar<T>(
        this IInlineInvokeProxy cph,
        string varName,
        bool persisted = true,
        T defaultValue = default
    ) =>
        cph.GetGlobalVar<string>(varName, persisted) is string stringValue
        && !string.IsNullOrEmpty(stringValue)
            ? JsonConvert.DeserializeObject<T>(stringValue) ?? defaultValue
            : defaultValue;

    public static void SetCustomGlobalVar<T>(
        this IInlineInvokeProxy cph,
        string varName,
        T value,
        bool persisted = true
    ) => cph.SetGlobalVar(varName, JsonConvert.SerializeObject(value), persisted);

    public static bool StreamIsLive(this IInlineInvokeProxy cph) =>
        cph.ObsIsConnected() && cph.ObsIsStreaming();

    public static IEnumerable<BaseUserInfo> GetStreamerBotAccounts(this IInlineInvokeProxy cph) =>
        (Broadcaster: cph.TwitchGetBroadcaster(), Bot: cph.TwitchGetBot()) switch
        {
            { Broadcaster: { } broadcaster, Bot: { } bot } => [broadcaster, bot],
            { Broadcaster: { } broadcaster } => [broadcaster],
            { Bot: { } bot } => [bot],
            _ => [],
        };

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

    private static bool TryParseDateTime(
        this string value,
        out DateTime result,
        string format = DateTimeFormat
    )
    {
        var success = DateTimeOffset.TryParseExact(
            value,
            format,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out var dateTimeOffset
        );

        result = success ? dateTimeOffset.UtcDateTime : default;
        return success;
    }
}

public static class BaseUserInfoExtensions
{
    public static string GetFormattedDisplayName(this BaseUserInfo user) =>
        user.HasLocalizedDisplayName()
            ? $"@{user.UserLogin} ({user.UserName})"
            : $"@{user.UserName}";

    public static bool HasLocalizedDisplayName(this BaseUserInfo user) =>
        !string.Equals(user.UserName, user.UserLogin, StringComparison.OrdinalIgnoreCase);
}

public static class DictionaryExtensions
{
    public static T GetUserFromArg<T>(
        this IDictionary<string, object> sbArgs,
        IInlineInvokeProxy cph,
        string argName = "user",
        T defaultValue = null
    )
        where T : BaseUserInfo =>
        sbArgs.TryGet(argName, out string argValue) ? cph.GetUser<T>(argValue) : defaultValue;

    public static T Get<T>(
        this IDictionary<string, object> sbArgs,
        string key,
        T defaultValue = default
    ) => sbArgs.TryGet(key, out T value) ? value : defaultValue;

    public static bool TryGet<T>(this IDictionary<string, object> sbArgs, string key, out T value)
    {
        // When argName is null or empty, throw an exception
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentNullException(nameof(key), "Argument name cannot be null or empty.");
        }

        // When key is not present in Dictionary, return false
        if (!sbArgs.TryGetValue(key, out var untypedValue))
        {
            value = default;
            return false;
        }

        // When T is an enum, attempt to parse as enum
        if (typeof(T).IsEnum)
        {
            return TryParseEnum(untypedValue, out value);
        }

        // Try to convert the untyped value from object to T
        return TryConvertValue(untypedValue, out value);
    }

    private static bool TryParseEnum<T>(object untypedValue, out T value)
    {
        try
        {
            value = (T)Enum.Parse(typeof(T), untypedValue.ToString(), ignoreCase: true);
            return true;
        }
        catch
        {
            value = default;
            return false;
        }
    }

    private static bool TryConvertValue<T>(object untypedValue, out T value)
    {
        // Attempt to cast from object to T using pattern matching
        if (untypedValue is T typedValue)
        {
            value = typedValue;
            return PassesNullCheck(value);
        }

        // Attempt to convert from object to T
        try
        {
            value = (T)Convert.ChangeType(untypedValue, typeof(T));
            return PassesNullCheck(value);
        }
        catch
        {
            // Attempt to convert from string to T
            try
            {
                value = (T)Convert.ChangeType(untypedValue.ToString(), typeof(T));
                return PassesNullCheck(value);
            }
            // Failed to cast/convert
            catch
            {
                value = default;
                return false;
            }
        }
    }

    private static bool PassesNullCheck<T>(T value) =>
        value is not null
        && (typeof(T) != typeof(string) || !string.IsNullOrEmpty(value.ToString()));
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
