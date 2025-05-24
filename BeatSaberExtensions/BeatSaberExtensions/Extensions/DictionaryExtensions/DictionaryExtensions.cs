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
        if (string.IsNullOrEmpty(argName))
        {
            throw new ArgumentNullException(
                nameof(argName),
                $"{nameof(argName)} cannot be null or empty."
            );
        }

        if (!sbArgs.TryGetValue(argName, out var untypedValue))
        {
            value = default;
            return false;
        }

        if (untypedValue is T typedValue)
        {
            value = typedValue;
            return true;
        }

        if (typeof(T) is { IsEnum: true } type)
        {
            try
            {
                value = (T)Enum.Parse(type, untypedValue.ToString());
                return true;
            }
            catch
            {
                value = default;
                return false;
            }
        }

        if (Convert.ChangeType(untypedValue.ToString(), typeof(T)) is T convertedValue)
        {
            value = convertedValue;
            return true;
        }

        value = default;
        return true;
    }
}
