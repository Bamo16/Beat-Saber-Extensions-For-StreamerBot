using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using BeatSaberExtensions.Extensions.StringExtensions;

namespace BeatSaberExtensions.Extensions.ExceptionExtensions;

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

        if (ex is AggregateException { InnerExceptions.Count: > 0 } aggEx)
            aggEx.FormatAggregateException(writer);
        else if (ex is { InnerException: { } innerEx })
            innerEx.FormatExceptionDetails(writer, isInner: true);

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

        var filteredFrames = new List<string>();

        foreach (var frame in frames)
            if (!_asyncMessageFilterPattern.IsMatch(frame))
                filteredFrames.Add(frame);

        if (filteredFrames is not { Count: > 0 })
            return;

        writer.WriteLine("Stack Trace:");

        foreach (var frame in filteredFrames)
            writer.WriteLine(frame);
    }
}
