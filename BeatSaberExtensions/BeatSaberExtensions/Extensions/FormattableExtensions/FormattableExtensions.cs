using System;

namespace BeatSaberExtensions.Extensions.FormattableExtensions;

public static class FormattableExtensions
{
    public static bool Equals<T1, T2>(this T1 value, T2 compareValue, string format = null)
        where T1 : IFormattable
        where T2 : IFormattable
    {
        var numFormat = format ?? value.GetNumberFormat();
        var valueStr = value.Format(format);
        var compareValueStr = compareValue.Format(numFormat);

        return valueStr == compareValueStr;
    }

    public static string ToOrdinal<T>(this T value, string format = null)
        where T : IFormattable => $"{value.Format(format)}{value.GetOrdinal()}";

    public static string Format<T>(this T value, string format = null)
        where T : IFormattable => value.ToString(format ?? value.GetNumberFormat(), null);

    private static string GetNumberFormat<T>(this T value)
        where T : IFormattable =>
        value switch
        {
            byte or sbyte or short or ushort or int or uint or long or ulong => "N0",
            float or double or decimal => "N3",
            _ => throw new ArgumentException("Value must be a numeric type.", nameof(value)),
        };

    private static string GetOrdinal<T>(this T value)
        where T : IFormattable =>
        value.ToPositiveWholeNumber() switch
        {
            var v when v % 100 is 11 or 12 or 13 => "ᵗʰ",
            var v when v % 10 is 1 => "ˢᵗ",
            var v when v % 10 is 2 => "ⁿᵈ",
            var v when v % 10 is 3 => "ʳᵈ",
            _ => "ᵗʰ",
        };

    private static long ToPositiveWholeNumber<T>(this T value, int digits = 0)
        where T : IFormattable => Math.Abs((long)Math.Round(Convert.ToDouble(value), digits));
}
