using System;
using System.Collections.Generic;

namespace BeatSaberExtensions.Extensions.FormattableExtensions;

public static class FormattableExtensions
{
    private static readonly Dictionary<int, string> _invertedCircleNumbers = new Dictionary<
        int,
        string
    >()
    {
        [0] = "⓪",
        [1] = "①",
        [2] = "②",
        [3] = "③",
        [4] = "④",
        [5] = "⑤",
        [6] = "⑥",
        [7] = "⑦",
        [8] = "⑧",
        [9] = "⑨",
        [10] = "⑩",
        [11] = "⑪",
        [12] = "⑫",
        [13] = "⑬",
        [14] = "⑭",
        [15] = "⑮",
        [16] = "⑯",
        [17] = "⑰",
        [18] = "⑱",
        [19] = "⑲",
        [20] = "⑳",
        [21] = "㉑",
        [22] = "㉒",
        [23] = "㉓",
        [24] = "㉔",
        [25] = "㉕",
        [26] = "㉖",
        [27] = "㉗",
        [28] = "㉘",
        [29] = "㉙",
        [30] = "㉚",
        [31] = "㉛",
        [32] = "㉜",
        [33] = "㉝",
        [34] = "㉞",
        [35] = "㉟",
        [36] = "㊱",
        [37] = "㊲",
        [38] = "㊳",
        [39] = "㊴",
        [40] = "㊵",
        [41] = "㊶",
        [42] = "㊷",
        [43] = "㊸",
        [44] = "㊹",
        [45] = "㊺",
        [46] = "㊻",
        [47] = "㊼",
        [48] = "㊽",
        [49] = "㊾",
        [50] = "㊿",
    };

    public static bool Equals<T1, T2>(this T1 value, T2 compareValue, string format = null)
        where T1 : IFormattable
        where T2 : IFormattable
    {
        var numFormat = format ?? value.GetNumberFormat();
        var valueStr = value.Format(format);
        var compareValueStr = compareValue.Format(numFormat);

        return valueStr == compareValueStr;
    }

    public static string ToInvertedCircleNumber<T>(this T value)
        where T : IFormattable =>
        value.ToPositiveWholeNumber() is var n
        && _invertedCircleNumbers.TryGetValue(n, out var result)
            ? result
            : n.ToString();

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

    private static int ToPositiveWholeNumber<T>(this T value, int digits = 0)
        where T : IFormattable => Math.Abs((int)Math.Round(Convert.ToDouble(value), digits));
}
