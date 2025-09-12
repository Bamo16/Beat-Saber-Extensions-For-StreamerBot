using System;
using System.Collections.Generic;

namespace BeatSaberExtensions.Extensions.DictionaryExtensions;

#nullable enable

public static class DictionaryExtensions
{
    public static T? Get<T>(
        this IDictionary<string, object> args,
        string key,
        T? defaultValue = default
    ) => args.TryGet(key, out T? value) ? value : defaultValue;

    public static bool TryGet<T>(this IDictionary<string, object> args, string key, out T? value)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentNullException(nameof(key), "Argument name cannot be null or empty.");
        }

        if (!args.TryGetValue(key, out var untypedValue) || untypedValue is null)
        {
            value = default;
            return false;
        }

        if (untypedValue is T typedValue)
        {
            value = typedValue;
            return value.PassesNullCheck();
        }

        return untypedValue.TryConvert(out value) && value.PassesNullCheck();
    }

    private static bool TryConvert<T>(
        this object untypedValue,
        out T? value,
        bool fromString = false
    )
    {
        try
        {
            value = (T)
                Convert.ChangeType(fromString ? untypedValue.ToString() : untypedValue, typeof(T));
            return true;
        }
        catch
        {
            if (!fromString)
            {
                return untypedValue.TryConvert(out value, fromString: true);
            }

            value = default;
            return false;
        }
    }

    private static bool PassesNullCheck<T>(this T value) =>
        value switch
        {
            { } when typeof(T) != typeof(string) => true,
            string str => !string.IsNullOrEmpty(str),
            _ => false,
        };
}
