using System;
using BeatSaberExtensions.Enums;
using BeatSaberExtensions.Extensions.TimeSpanExtensions;

namespace BeatSaberExtensions.Extensions.DateTimeExtensions;

public static class DateTimeExtensions
{
    public static string ToCurrentDateAwareFriendlyFormat(this DateTime value) =>
        value switch
        {
            _ when value.IsInLast24Hours() =>
                $"{DateTime.UtcNow.Subtract(value).ToFriendlyString(TimeUnit.Minute)} ago",
            _ when value.IsInCurrentMonth() =>
                $"{DateTime.UtcNow.Subtract(value).ToFriendlyString(TimeUnit.Hour)} ago",
            _ when value.IsInCurrentYear() => $"on {value:MMMM d}",
            _ => $"on {value:MMMM d yyyy}",
        };

    private static bool IsInLast24Hours(this DateTime dateTime) =>
        DateTime.UtcNow.Subtract(dateTime) is { TotalHours: < 24 };

    private static bool IsInCurrentMonth(this DateTime dateTime) =>
        DateTime.UtcNow is { Year: var currentYear, Month: var currentMonth }
        && dateTime.Year == currentYear
        && dateTime.Month == currentMonth;

    private static bool IsInCurrentYear(this DateTime dateTime) =>
        dateTime.Year == DateTime.UtcNow.Year;
}
