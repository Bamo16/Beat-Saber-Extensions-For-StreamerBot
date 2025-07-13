using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using BeatSaberExtensions.Extensions.BaseUserInfoExtensions;
using BeatSaberExtensions.Extensions.GroupUserExtensions;
using BeatSaberExtensions.Extensions.StringExtensions;
using BeatSaberExtensions.Utility.Logging;
using Streamer.bot.Plugin.Interface;
using Streamer.bot.Plugin.Interface.Enums;
using Streamer.bot.Plugin.Interface.Model;

namespace BeatSaberExtensions.Extensions.InlineInvokeProxyExtensions;

public static class InlineInvokeProxyExtensions
{
    private const string LocalizedDisplayUsersGroup = "Localized DisplayName Users";

    private static readonly ConcurrentDictionary<string, BaseUserInfo> _userLookup =
        new ConcurrentDictionary<string, BaseUserInfo>(StringComparer.OrdinalIgnoreCase);

    private static TwitchUserInfo _broadcaster;
    private static TwitchUserInfo _bot;

    public static T GetUser<T>(this IInlineInvokeProxy cph, string lookupString)
        where T : BaseUserInfo
    {
        var sanitized = (lookupString ?? string.Empty).Trim().TrimStart('@');

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return null;
        }

        if (cph.GetCachedUser(sanitized) is { } cached)
        {
            return typeof(T) == typeof(BaseUserInfo)
                // Fast path: already cached, and T is BaseUserInfo
                ? cached as T
                // Fetch from Twitch API when T is TwitchUserInfo or TwitchUserInfoEx
                : cph.GetTwitchUserByLogin<T>(cached.UserLogin);
        }

        if (cph.GetTwitchUserByLogin<T>(sanitized) is { } user)
        {
            // Cache BaseUserInfo value when user is fetched successfully
            return user.CacheUser();
        }

        return null;
    }

    public static void SendMessageAndLog(
        this IInlineInvokeProxy cph,
        string message,
        bool useBot = true,
        bool fallback = true
    )
    {
        if (!string.IsNullOrEmpty(message))
        {
            Logger.Log($"Sending message: \"{message}\".");

            cph.SendMessage(message, useBot, fallback);
        }
    }

    public static DateTime GetDateTimeGlobalVar(
        this IInlineInvokeProxy cph,
        string varName,
        bool persisted = true,
        DateTime defaultValue = default
    ) =>
        cph.GetGlobalVar<string>(varName, persisted).TryParseDateTime(out var value)
            ? value
            : defaultValue;

    public static void SetDateTimeGlobalVar(
        this IInlineInvokeProxy cph,
        string varName,
        DateTime? value = null,
        bool persisted = true
    ) =>
        cph.SetGlobalVar(
            varName,
            (value ?? DateTime.UtcNow).ToLocalTime().ToString("yyyy-MM-dd hh:mm:ss.fff tt zzz"),
            persisted
        );

    public static void SetCommandState(this IInlineInvokeProxy cph, string commandId, bool state)
    {
        if (state)
            cph.EnableCommand(commandId);
        else
            cph.DisableCommand(commandId);
    }

    public static bool StreamIsLive(this IInlineInvokeProxy cph) =>
        cph.ObsIsConnected() && cph.ObsIsStreaming();

    public static bool TryWaitForCondition<T>(
        this IInlineInvokeProxy cph,
        Func<T> valueFactory,
        Func<T, bool> predicate,
        out T result,
        int timeoutMs = 15000,
        int pollingIntervalMs = 100
    )
    {
        var timeoutTime = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow < timeoutTime)
        {
            result = valueFactory.Invoke();

            if (predicate.Invoke(result))
            {
                return true;
            }

            if (DateTime.UtcNow.AddMilliseconds(pollingIntervalMs) <= timeoutTime)
                cph.Wait(pollingIntervalMs);
            else
                break;
        }

        result = default;

        return false;
    }

    public static bool IsBroadcasterLogin(this IInlineInvokeProxy cph, string userLogin) =>
        cph.GetBroadcaster().UserLogin == userLogin;

    public static bool EnsureGroupMembershipForUser(
        this IInlineInvokeProxy cph,
        BaseUserInfo user,
        string groupName,
        bool shouldBelongToGroup
    )
    {
        var displayName = user.GetFormattedDisplayName();
        var groupStatus = shouldBelongToGroup ? "belongs to" : "does not belong to";

        Logger.Log($"Ensuring that {displayName} {groupStatus} the \"{groupName}\" group.");

        if (!cph.EnsureGroupState(groupName, true) || user is not { UserId: { } userId })
        {
            return false;
        }

        if (cph.UserIdInGroup(userId, Platform.Twitch, groupName) == shouldBelongToGroup)
        {
            var membershipDescriptor = shouldBelongToGroup ? "a member of" : "not a member of";

            Logger.Log(
                $"{displayName} is already {membershipDescriptor} the \"{groupName}\" group."
            );

            return true;
        }

        var actionVerb = shouldBelongToGroup ? "add" : "remove";
        var success = shouldBelongToGroup
            ? cph.AddUserIdToGroup(userId, Platform.Twitch, groupName)
            : cph.RemoveUserIdFromGroup(userId, Platform.Twitch, groupName);

        if (success)
        {
            Logger.Log(
                $"Successfully {actionVerb}ed {displayName} {(shouldBelongToGroup ? "to" : "from")} the \"{groupName}\" group."
            );

            return true;
        }

        Logger.LogError(
            $"Failed to {actionVerb} {displayName} {(shouldBelongToGroup ? "to" : "from")} the \"{groupName}\" group."
        );

        return false;
    }

    public static bool EnsureGroupState(
        this IInlineInvokeProxy cph,
        string groupName,
        bool groupExists
    ) =>
        cph.GroupExists(groupName) == groupExists
        || (groupExists ? cph.AddGroup(groupName) : cph.DeleteGroup(groupName));

    public static TwitchUserInfo GetBroadcaster(this IInlineInvokeProxy cph) =>
        _broadcaster ??= cph.TwitchGetBroadcaster();

    public static TwitchUserInfo GetBot(this IInlineInvokeProxy cph) => _bot ??= cph.TwitchGetBot();

    private static BaseUserInfo GetCachedUser(this IInlineInvokeProxy cph, string lookupString)
    {
        if (_userLookup.TryGetValue(lookupString, out var user))
        {
            return user;
        }

        if (!lookupString.ContainsNonAscii())
        {
            return null;
        }

        foreach (var localizedUser in cph.GetBaseUserInfoInGroup(LocalizedDisplayUsersGroup))
            localizedUser.CacheUser();

        return _userLookup.TryGetValue(lookupString, out user) ? user : null;
    }

    private static List<BaseUserInfo> GetBaseUserInfoInGroup(
        this IInlineInvokeProxy cph,
        string groupName
    ) =>
        cph.EnsureGroupState(groupName, true)
            ? cph.UsersInGroup(groupName).ConvertAll(groupUser => groupUser.ToBaseUserInfo())
            : throw new InvalidOperationException($"Failed to create group: \"{groupName}\".");

    private static T GetTwitchUserByLogin<T>(this IInlineInvokeProxy cph, string userLogin)
        where T : BaseUserInfo =>
        typeof(T) == typeof(TwitchUserInfoEx)
            ? cph.TwitchGetExtendedUserInfoByLogin(userLogin) as T
            : cph.TwitchGetUserInfoByLogin(userLogin) as T;

    private static T CacheUser<T>(this T user)
        where T : BaseUserInfo
    {
        if (user is { UserLogin: { } username, UserName: { } displayName })
        {
            _userLookup.TryAdd(username, user);
            _userLookup.TryAdd(displayName, user);
        }

        return user;
    }
}
