using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BeatSaberExtensions.Enums;
using BeatSaberExtensions.Extensions.ExceptionExtensions;
using BeatSaberExtensions.Extensions.StringExtensions;
using Newtonsoft.Json;
using Streamer.bot.Common.Events;
using Streamer.bot.Plugin.Interface;

namespace BeatSaberExtensions.Utility.Logging;

public class StreamerBotLogger(
    IInlineInvokeProxy cph,
    string logMessageTag,
    LogAction defaultLogAction = LogAction.Info,
    int afterChars = 1000,
    int truncateAfterCharsError = 3000
)
{
    private static readonly object _lock = new();

    #region Explicit Log Action Logger Methods

    public void LogDebug(
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

    #endregion

    #region Action Start Logger Methods

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

    #endregion

    #region Action Completion Logger Methods

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

    #endregion

    #region Object Logger Methods

    public void LogObject(
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

    #endregion

    #region Exception Logger Methods

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

    #endregion

    #region General Logger Methods

    public void Log(
        string logLine,
        LogAction? logAction = null,
        int? truncateAfterChars = null,
        [CallerMemberName] string methodName = null,
        [CallerLineNumber] int lineNumber = 0
    ) => (
            (logAction ?? defaultLogAction) switch
            {
                _ when cph is null => _ => { },
                LogAction.Debug => cph.LogDebug,
                LogAction.Verbose => cph.LogVerbose,
                LogAction.Info => cph.LogInfo,
                LogAction.Warn => cph.LogWarn,
                LogAction.Error => cph.LogError,
                _ => new Action<string>(_ => { }),
            }
        )(Truncate($"[{logMessageTag}] [{methodName} L{lineNumber}] {logLine}", logAction, truncateAfterChars));

    #endregion

    #region Private Methods

    private string Truncate(
        string logLine,
        LogAction? logAction = null,
        int? truncateAfterChars = null
    ) =>
        logLine.Truncate(
            truncateAfterChars
                ?? (logAction is LogAction.Error ? truncateAfterCharsError : afterChars)
        );

    #endregion
}
