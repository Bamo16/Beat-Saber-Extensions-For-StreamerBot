using System;
using System.Collections.Generic;

namespace BeatSaberExtensions.Extensions.DictionaryExtensions;

public static class DictionaryExtensions
{
    public static T Get<T>(
        this IDictionary<string, object> sbArgs,
        string key,
        T defaultValue = default
    ) => sbArgs.TryGet(key, out T value) ? value : defaultValue;

    public static bool TryGet<T>(this IDictionary<string, object> sbArgs, string key, out T value)
    {
        // When argName is null or empty, throw an exception
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentNullException(nameof(key), "Argument name cannot be null or empty.");
        }

        // When key is not present in Dictionary, return false
        if (!sbArgs.TryGetValue(key, out var untypedValue))
        {
            value = default;
            return false;
        }

        // When T is an enum, attempt to parse as enum
        if (typeof(T).IsEnum)
        {
            return TryParseEnum(untypedValue, out value);
        }

        // Try to convert the untyped value from object to T
        return TryConvertValue(untypedValue, out value);
    }

    private static bool TryParseEnum<T>(object untypedValue, out T value)
    {
        try
        {
            value = (T)Enum.Parse(typeof(T), untypedValue.ToString(), ignoreCase: true);
            return true;
        }
        catch
        {
            value = default;
            return false;
        }
    }

    private static bool TryConvertValue<T>(object untypedValue, out T value)
    {
        // Attempt to cast from object to T using pattern matching
        if (untypedValue is T typedValue)
        {
            value = typedValue;
            return PassesNullCheck(value);
        }

        // Attempt to convert from object to T
        try
        {
            value = (T)Convert.ChangeType(untypedValue, typeof(T));
            return PassesNullCheck(value);
        }
        catch
        {
            // Attempt to convert from string to T
            try
            {
                value = (T)Convert.ChangeType(untypedValue.ToString(), typeof(T));
                return PassesNullCheck(value);
            }
            // Failed to cast/convert
            catch
            {
                value = default;
                return false;
            }
        }
    }

    private static bool PassesNullCheck<T>(T value) =>
        value is not null
        && (typeof(T) != typeof(string) || !string.IsNullOrEmpty(value.ToString()));
}
