using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BeatSaberExtensions.Enums;
using BeatSaberExtensions.Extensions.ExceptionExtensions;
using BeatSaberExtensions.Extensions.StringExtensions;
using BeatSaberExtensions.Utility.Arguments;
using Newtonsoft.Json;
using Streamer.bot.Plugin.Interface;

namespace BeatSaberExtensions.Utility.Logging;

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

        // Any instance of !bsr Raider Request triggered by the broadcaster account can be safely ignored.
        // Setting executeSuccess to true results in the action stopping immediately.
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
        )(
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
