using System;
using System.Collections.Concurrent;
using BeatSaberExtensions.Extensions.StringExtensions;

namespace BeatSaberExtensions.Extensions.InlineInvokeProxyExtensions;

public static class TwitchUserLookup
{
    private const string LocalizedDisplayUsersGroup = "Localized DisplayName Users";

    private static readonly object _lock = new object();
    private static readonly ConcurrentDictionary<string, BaseUserInfo> _cachedByName =
        new ConcurrentDictionary<string, BaseUserInfo>(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, BaseUserInfo> _cachedById =
        new ConcurrentDictionary<string, BaseUserInfo>(StringComparer.OrdinalIgnoreCase);

    private static bool _isInitialized = false;
    private static TwitchUserInfo _broadcaster;
    private static TwitchUserInfo _bot;

    public static TwitchUserInfo GetBroadcaster(this IInlineInvokeProxy cph) =>
        _broadcaster ??= cph.TwitchGetBroadcaster();

    public static TwitchUserInfo GetBot(this IInlineInvokeProxy cph) => _bot ??= cph.TwitchGetBot();

    public static T GetUser<T>(this IInlineInvokeProxy cph, string input, bool isUserId = false)
        where T : BaseUserInfo =>
        cph.GetCachedUser(input) switch
        {
            { } cached when typeof(T) == typeof(BaseUserInfo) => cached as T,

            { UserId: { } id } => cph.GetUserInternal<T>(id, isUserId: true),

            _ => cph.GetUserInternal<T>(input, isUserId),
        };

    private static T GetUserInternal<T>(
        this IInlineInvokeProxy cph,
        string input,
        bool isUserId = false
    )
        where T : BaseUserInfo
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return default;
        }

        var sanitized = input.SanitizeLookupString();

        if (typeof(T) == typeof(TwitchUserInfoEx))
        {
            return isUserId
                ? cph.TwitchGetExtendedUserInfoById(sanitized) as T
                : cph.TwitchGetExtendedUserInfoByLogin(sanitized) as T;
        }

        return isUserId
            ? cph.TwitchGetUserInfoById(sanitized) as T
            : cph.TwitchGetUserInfoByLogin(sanitized) as T;
    }

    private static BaseUserInfo GetCachedUser(
        this IInlineInvokeProxy cph,
        string input,
        bool isUserId = false
    )
    {
        if (!_isInitialized)
        {
            lock (_lock)
            {
                if (!_isInitialized)
                {
                    foreach (var user in cph.BaseUserInfoInGroup(LocalizedDisplayUsersGroup))
                        user.CacheUser();

                    _isInitialized = true;
                }
            }
        }

        var cacheDict = isUserId ? _cachedById : _cachedByName;

        return cacheDict.TryGetValue(input.SanitizeLookupString(), out var cached)
            ? cached
            : default;
    }

    private static T CacheUser<T>(this T user)
        where T : BaseUserInfo
    {
        if (user is null)
            return user;

        _cachedById.AddOrUpdate(user.UserId, _ => user, (_, _) => user);
        _cachedByName.AddOrUpdate(user.UserLogin, _ => user, (_, _) => user);
        _cachedByName.AddOrUpdate(user.UserName, _ => user, (_, _) => user);

        return user;
    }
}
