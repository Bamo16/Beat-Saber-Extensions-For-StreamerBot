using System;
using System.Linq;
using BeatSaberExtensions.Enums;
using BeatSaberExtensions.Extensions.StringExtensions;

namespace BeatSaberExtensions.Extensions.TimeSpanExtensions;

public static class TimeSpanExtensions
{
    public static string Format(this TimeSpan timeSpan, TimeUnit precision = TimeUnit.Second)
    {
        var parts = new (TimeUnit Unit, int Value)[]
        {
            (TimeUnit.Day, (int)timeSpan.TotalDays),
            (TimeUnit.Hour, timeSpan.Hours),
            (TimeUnit.Minute, timeSpan.Minutes),
            (TimeUnit.Second, timeSpan.Seconds),
        }
            .Where(p => p is { Value: not 0 } && p.Unit <= precision)
            .DefaultIfEmpty((Unit: precision, Value: 0))
            .Select(p => p.Unit.ToString().Pluralize(p.Value, toLower: true))
            .ToList();

        return parts.Count switch
        {
            1 => parts[0],
            2 => $"{parts[0]} and {parts[1]}",
            _ => $"{string.Join(", ", parts.Take(parts.Count - 1))}, and {parts.Last()}",
        };
    }
}
