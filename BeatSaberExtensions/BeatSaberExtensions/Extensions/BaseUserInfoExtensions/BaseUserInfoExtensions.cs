using System;
using BeatSaberExtensions.Enums;
using BeatSaberExtensions.Utility;
using Streamer.bot.Plugin.Interface.Model;

namespace BeatSaberExtensions.Extensions.BaseUserInfoExtensions;

public static class BaseUserInfoExtensions
{
    public static string Format(this BaseUserInfo user, UsernameDisplayMode? mode = null)
    {
        return user is not { UserLogin: { } login, UserName: { } display }
            ? "UnknownUser"
            : (Mode: mode ?? UserConfig.Config.UsernameDisplayMode, Localized: user.IsLocalized()) switch
            {
                // When mode is UserLoginOnly and DisplayName is localized, show LoginName
                { Mode: UsernameDisplayMode.UserLoginOnly, Localized: true } => $"@{login}",

                // When mode is UserLoginOnly and DisplayName is not localized, show DisplayName with proper capitalization
                { Mode: UsernameDisplayMode.UserLoginOnly, Localized: false } => $"@{display}",

                // When mode is DisplayNameOnly, only show DisplayName
                { Mode: UsernameDisplayMode.DisplayNameOnly } => $"@{display}",

                // When mode is Dynamic and DisplayName is localized, show DisplayName with LoginName in parentheses
                { Mode: UsernameDisplayMode.Dynamic, Localized: true } => $"@{display} ({login})",

                // When mode is Dynamic and DisplayName is not localized, only show DisplayName
                { Mode: UsernameDisplayMode.Dynamic, Localized: false } => $"@{display}",

                // Failure case
                { Mode: var m } => throw new InvalidOperationException(
                    $"Unsupported UsernameDisplayMode: {m}."
                ),
            };
    }

    public static bool IsLocalized(this BaseUserInfo user) =>
        !string.Equals(user.UserName, user.UserLogin, StringComparison.OrdinalIgnoreCase);
}
