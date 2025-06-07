using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BeatSaberExtensions.Enums;
using BeatSaberExtensions.Extensions.DictionaryExtensions;
using BeatSaberExtensions.Extensions.ExceptionExtensions;
using BeatSaberExtensions.Extensions.InlineInvokeProxyExtensions;
using BeatSaberExtensions.Extensions.StringExtensions;
using Newtonsoft.Json;
using Streamer.bot.Common.Events;
using Streamer.bot.Plugin.Interface;

namespace BeatSaberExtensions.Utility.Logging;

public static class Logger
{
    private static IInlineInvokeProxy _cph;
    private static string _logMessageTag;
    private static LogAction _defaultLogAction;
    private static int _truncateAfterChars;
    private static int _truncateAfterCharsError;

    public static bool IsInitialized => _cph is not null;

    public static void Init(
        IInlineInvokeProxy cph,
        string logMessageTag,
        LogAction defaultLogAction = LogAction.Info,
        int truncateAfterChars = 1000,
        int truncateAfterCharsError = 3000
    )
    {
        _cph = cph;
        _logMessageTag = logMessageTag;
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

    public static void LogActionStart(
        Dictionary<string, object> args,
        out bool executeSuccess,
        out Dictionary<string, object> sbArgs,
        out EventType eventType,
        [CallerMemberName] string methodName = null,
        [CallerLineNumber] int lineNumber = 0
    )
    {
        sbArgs = new Dictionary<string, object>(args);
        eventType = _cph.GetEventType();
        var userLogin = sbArgs.GetArgOrDefault("userName", string.Empty);
        var commandId = sbArgs.GetArgOrDefault("commandId", string.Empty);

        // Any instance of !bsr Raider Request triggered by the broadcaster account can be safely ignored.
        // Setting executeSuccess to true results in the action stopping immediately.
        if (_cph.IsBroadcasterLogin(userLogin) && commandId is UserConfig.RaidRequestCommandId)
        {
            executeSuccess = true;
            SetExecuteSuccessArgument(executeSuccess);

            return;
        }

        executeSuccess = false;

        LogObject(
            new
            {
                EventType = eventType.ToString(),
                CommandId = commandId,
                Command = sbArgs.GetArgOrDefault("command", string.Empty),
                RawInput = sbArgs.GetArgOrDefault("rawInput", string.Empty),
                UserLogin = userLogin,
            },
            "Action started with",
            methodName: methodName,
            lineNumber: lineNumber
        );
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
    ) => (
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
        )(Truncate($"[{_logMessageTag}] [{methodName} L{lineNumber}] {logLine}", logAction, truncateAfterChars));

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
