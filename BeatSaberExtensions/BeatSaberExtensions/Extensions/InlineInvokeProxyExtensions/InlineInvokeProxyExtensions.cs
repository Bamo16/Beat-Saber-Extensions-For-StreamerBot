using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BeatSaberExtensions.Extensions.DictionaryExtensions;
using BeatSaberExtensions.Extensions.ExceptionExtensions;
using BeatSaberExtensions.Extensions.StringExtensions;
using Streamer.bot.Plugin.Interface;
using Streamer.bot.Plugin.Interface.Enums;
using Streamer.bot.Plugin.Interface.Model;

namespace BeatSaberExtensions.Extensions.InlineInvokeProxyExtensions;

public static class InlineInvokeProxyExtensions
{
    private const string LocalizedDisplayUsersGroup = "Localized DisplayName Users";

    private static readonly ConcurrentDictionary<string, BaseUserInfo> _userLookup = new(
        StringComparer.OrdinalIgnoreCase
    );

    public static T GetUserInfoFromArgs<T>(
        this IInlineInvokeProxy cph,
        Dictionary<string, object> sbArgs,
        string argName = "userName",
        string fallbackArgName = null
    )
        where T : BaseUserInfo =>
        sbArgs.TryGetArg(argName, out string userLogin) && cph.GetUserInfo<T>(userLogin) is { } user
            ? user
        : !string.IsNullOrEmpty(fallbackArgName)
        && sbArgs.TryGetArg(fallbackArgName, out string fallbackUserLogin)
        && cph.GetUserInfo<T>(fallbackUserLogin) is { } fallbackUser
            ? fallbackUser
        : null;

    public static T GetUserInfo<T>(this IInlineInvokeProxy cph, string lookupString)
        where T : BaseUserInfo
    {
        if (string.IsNullOrWhiteSpace(lookupString))
            return null;

        var sanitized = lookupString.Trim().TrimStart('@');

        if (sanitized.ContainsNonAscii() && !_userLookup.ContainsKey(sanitized))
            cph.LoadLocalizedDisplayNameUsers();

        if (_userLookup.TryGetValue(sanitized, out var cachedUser))
        {
            if (cachedUser is T typedCachedUser)
                return typedCachedUser;

            return cph.GetTwitchUserByLogin<T>(cachedUser.UserLogin);
        }

        var user = cph.GetTwitchUserByLogin<T>(sanitized);
        if (user is { UserLogin: { } userLogin })
            _userLookup[userLogin] = user;

        return user;
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
        var timeoutTime = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMs);

        while (DateTime.UtcNow < timeoutTime)
        {
            result = valueFactory.Invoke();

            if (predicate.Invoke(result))
                return true;

            cph.Wait(pollingIntervalMs);
        }

        result = default;

        return false;
    }

    public static bool EnsureGroupMembershipForUser(
        this IInlineInvokeProxy cph,
        BaseUserInfo user,
        string groupName,
        bool shouldBelongToGroup
    ) =>
        cph.EnsureGroupState(groupName, true)
        && user is { UserId: { } userId }
        && (
            cph.UserIdInGroup(userId, Platform.Twitch, groupName) == shouldBelongToGroup
            || (
                shouldBelongToGroup
                    ? cph.AddUserIdToGroup(userId, Platform.Twitch, groupName)
                    : cph.RemoveUserIdFromGroup(userId, Platform.Twitch, groupName)
            )
        );

    public static bool EnsureGroupState(
        this IInlineInvokeProxy cph,
        string groupName,
        bool groupExists
    ) =>
        cph.GroupExists(groupName) == groupExists
        || (groupExists ? cph.AddGroup(groupName) : cph.DeleteGroup(groupName));

    public static void HandleNullLoggerOnInit(
        this IInlineInvokeProxy cph,
        Exception ex,
        string loggerName,
        string actionName,
        [CallerMemberName] string callerName = null
    )
    {
        var aggEx = new AggregateException(
            ex,
            new ArgumentNullException(loggerName, $"{loggerName} is null.")
        );

        cph.LogError(
            $"[{actionName}] [{callerName ?? "Unknown Method"}] {aggEx.GetFormattedExceptionMessage()}"
        );
    }

    private static void LoadLocalizedDisplayNameUsers(this IInlineInvokeProxy cph)
    {
        cph.EnsureGroupState(LocalizedDisplayUsersGroup, true);

        foreach (var groupUser in cph.UsersInGroup(LocalizedDisplayUsersGroup))
        {
            if (
                groupUser is not { Login: { } login, Username: { } userName }
                || _userLookup.ContainsKey(userName)
                || cph.GetTwitchUserByLogin<BaseUserInfo>(login)
                    is not { UserLogin: { } userLogin, UserName: { } displayName } user
            )
                continue;
            _userLookup[displayName] = user;

            _userLookup.TryAdd(userLogin, user);
        }
    }

    private static T GetTwitchUserByLogin<T>(this IInlineInvokeProxy cph, string userLogin)
        where T : BaseUserInfo =>
        typeof(T) == typeof(TwitchUserInfoEx)
            ? cph.TwitchGetExtendedUserInfoByLogin(userLogin) as T
            : cph.TwitchGetUserInfoByLogin(userLogin) as T;
}
