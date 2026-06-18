using System;
using System.Linq;
using System.Runtime.CompilerServices;
using BeatSaberExtensions.Enums;
using BeatSaberExtensions.Extensions.BaseUserInfoExtensions;
using BeatSaberExtensions.Extensions.ExceptionExtensions;
using BeatSaberExtensions.Extensions.StringExtensions;
using BeatSaberExtensions.Utility.Arguments;
using Newtonsoft.Json;

namespace BeatSaberExtensions.Utility.Logging;

#nullable enable

public static class Logger
{
    private const char BraillePatternBlankChar = '\u2800';

    private static IInlineInvokeProxy? _cph;
    private static string? _logMessageTag;
    private static LogAction _defaultLogAction;
    private static int _truncateAfterChars;
    private static int _truncateAfterCharsError;

    private static IInlineInvokeProxy CPH =>
        _cph ?? throw new InvalidOperationException($"{nameof(CPH)} is null.");

    public static void Init(
        IInlineInvokeProxy cph,
        string logMessageTag,
        LogAction defaultLogAction = LogAction.Info,
        int truncateAfterChars = 1000,
        int truncateAfterCharsError = 5000
    )
    {
        _cph = cph;
        _logMessageTag = logMessageTag;
        _defaultLogAction = defaultLogAction;
        _truncateAfterChars = truncateAfterChars;
        _truncateAfterCharsError = truncateAfterCharsError;

        Log("Logger Initialized");
    }

    public static void LogDebug(
        string logLine,
        int? truncateAfter = null,
        [CallerMemberName] string? methodName = null,
        [CallerLineNumber] int lineNumber = 0
    ) => Log(logLine, LogAction.Debug, truncateAfter, methodName, lineNumber);

    public static void LogVerbose(
        string logLine,
        int? truncateAfter = null,
        [CallerMemberName] string? methodName = null,
        [CallerLineNumber] int lineNumber = 0
    ) => Log(logLine, LogAction.Verbose, truncateAfter, methodName, lineNumber);

    public static void LogInfo(
        string logLine,
        int? truncateAfter = null,
        [CallerMemberName] string? methodName = null,
        [CallerLineNumber] int lineNumber = 0
    ) => Log(logLine, LogAction.Info, truncateAfter, methodName, lineNumber);

    public static void LogWarn(
        string logLine,
        int? truncateAfter = null,
        [CallerMemberName] string? methodName = null,
        [CallerLineNumber] int lineNumber = 0
    ) => Log(logLine, LogAction.Warn, truncateAfter, methodName, lineNumber);

    public static void LogError(
        string logLine,
        int? truncateAfter = null,
        [CallerMemberName] string? methodName = null,
        [CallerLineNumber] int lineNumber = 0
    ) => Log(logLine, LogAction.Error, truncateAfter, methodName, lineNumber);

    public static void LogObject(
        object logObject,
        string? label = null,
        LogAction? logAction = null,
        int? truncateAfterChars = null,
        [CallerMemberName] string? methodName = null,
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

    public static void LogSendResponse(
        string message,
        string label,
        LogAction? logAction = null,
        int? truncateAfterChars = null,
        [CallerMemberName] string? methodName = null,
        [CallerLineNumber] int lineNumber = 0
    )
    {
        var isMultiline = message.Contains('\n');
        var logContext = isMultiline
            ? string.Join(
                Environment.NewLine,
                message
                    .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => "\t" + line.TrimEnd(BraillePatternBlankChar))
            )
            : $" \"{message.TrimEnd(BraillePatternBlankChar)}\"";
        var separator = isMultiline ? Environment.NewLine : " ";
        var logLine = $"{label}:{separator}{logContext}".ReplaceVerticalWhitespace();

        Log(logLine, logAction, truncateAfterChars, methodName, lineNumber);
    }

    public static void LogActionStart(
        this ActionContext context,
        string label = "Action started with",
        [CallerMemberName] string? methodName = null,
        [CallerLineNumber] int lineNumber = 0
    )
    {
        LogObject(
            new (string Key, string? Value)[]
            {
                (
                    "EventType",
                    context switch
                    {
                        {
                            EventType: { } eventType and EventType.CommandTriggered,
                            CommandType: { } cmdType
                        } => $"{eventType} ({cmdType})",
                        { EventType: { } eventType } => $"{eventType}",
                    }
                ),
                ("Command", context.Command),
                ("RawInput", context.RawInput),
                ("Caller", context.Caller?.Format()),
            }
                .Where(kvp => kvp is { Value.Length: > 0 })
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            label,
            methodName: methodName,
            lineNumber: lineNumber
        );
    }

    public static void LogActionCompletion(
        bool executeSuccess,
        [CallerMemberName] string? methodName = null,
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

    public static void HandleException(
        Exception ex,
        bool setArgument = true,
        string argumentName = "CSharpErrorMessage",
        LogAction logAction = LogAction.Error,
        int? truncateAfter = null,
        [CallerMemberName] string? methodName = null,
        [CallerLineNumber] int lineNumber = 0
    )
    {
        UserConfig.AddRecentExceptionMessage(
            $"{ex.GetType().Name} in {ex.FormatMemberName(methodName)}: {ex.Message}"
        );

        var message = ex.Format();

        if (setArgument)
        {
            var argumentValue = CPH.TryGetArg<string>(argumentName, out var currentValue)
                ? string.Join("; ", currentValue, message)
                : message;

            CPH.SetArgument(argumentName, argumentValue);
        }

        Log(message, logAction, truncateAfter ?? _truncateAfterCharsError, methodName, lineNumber);
    }

    public static void Log(
        string logLine,
        LogAction? logAction = null,
        int? truncateAfterChars = null,
        [CallerMemberName] string? methodName = null,
        [CallerLineNumber] int lineNumber = 0
    ) =>
        (
            (logAction ?? _defaultLogAction) switch
            {
                LogAction.Debug => CPH.LogDebug,
                LogAction.Verbose => CPH.LogVerbose,
                LogAction.Info => CPH.LogInfo,
                LogAction.Warn => CPH.LogWarn,
                LogAction.Error => CPH.LogError,
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
        CPH.SetArgument("ExecuteSuccess", executeSuccess);
}
