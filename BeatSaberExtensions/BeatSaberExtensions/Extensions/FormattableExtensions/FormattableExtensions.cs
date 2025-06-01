using System;

namespace BeatSaberExtensions.Extensions.FormattableExtensions;

public static class FormattableExtensions
{
    public static bool Equals<T1, T2>(this T1 value, T2 compareValue, string format = null)
        where T1 : IFormattable
        where T2 : IFormattable
    {
        var numFormat = format ?? value.GetDefaultNumberFormat();
        var valueStr = value.ToString(numFormat, null);
        var compareValueStr = compareValue.ToString(numFormat, null);

        return valueStr == compareValueStr;
    }

    private static string GetDefaultNumberFormat<T>(this T value)
        where T : IFormattable =>
        value switch
        {
            byte or sbyte or short or ushort or int or uint or long or ulong => "N0",
            float or double or decimal => "N3",
            _ => throw new ArgumentException("Value must be a numeric type.", nameof(value)),
        };
}
