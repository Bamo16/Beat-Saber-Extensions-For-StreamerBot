using System;
using System.Linq;
using BeatSaberExtensions.Enums;
using BeatSaberExtensions.Extensions.StringExtensions;

namespace BeatSaberExtensions.Extensions.TimeSpanExtensions;

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
        where T : IFormattable => unit.ToString().ToLowerInvariant().Pluralize(value);
}
