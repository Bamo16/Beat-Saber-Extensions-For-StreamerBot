using System;
using System.Collections.Generic;

namespace BeatSaberExtensions.Extensions.DictionaryExtensions;

public static class DictionaryExtensions
{
    public static T GetArgOrDefault<T>(
        this IDictionary<string, object> sbArgs,
        string argName,
        T defaultValue = default
    ) => sbArgs.TryGetArg(argName, out T value) ? value : defaultValue;

    public static bool TryGetArg<T>(
        this IDictionary<string, object> sbArgs,
        string argName,
        out T value
    )
    {
        // When argName is null or empty, throw an exception
        if (string.IsNullOrEmpty(argName))
        {
            throw new ArgumentNullException(
                nameof(argName),
                $"{nameof(argName)} cannot be null or empty."
            );
        }

        // When key is not present in Dictionary, return false
        if (!sbArgs.TryGetValue(argName, out var untypedValue))
        {
            value = default;
            return false;
        }

        // When T is an enum, attempt to parse as enum
        if (typeof(T) is { IsEnum: true } type)
        {
            try
            {
                value = (T)Enum.Parse(type, untypedValue.ToString(), ignoreCase: true);
                return true;
            }
            catch
            {
                value = default;
                return false;
            }
        }

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
