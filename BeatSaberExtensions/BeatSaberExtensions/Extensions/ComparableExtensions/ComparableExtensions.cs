using System;

namespace BeatSaberExtensions.Extensions.ComparableExtensions;

public static class ComparableExtensions
{
    public static T Clamp<T>(this T value, T min, T max)
        where T : IComparable<T>
    {
        if (value.CompareTo(min) < 0)
        {
            return min;
        }

        if (value.CompareTo(max) > 0)
        {
            return max;
        }

        return value;
    }
}
