using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatSaberExtensions.Extensions.BaseUserInfoExtensions;
using BeatSaberExtensions.Extensions.DateTimeExtensions;
using BeatSaberExtensions.Extensions.GroupUserExtensions;
using BeatSaberExtensions.Extensions.StringExtensions;
using BeatSaberExtensions.Utility.Logging;
using Newtonsoft.Json;

namespace BeatSaberExtensions.Extensions.InlineInvokeProxyExtensions;

public static class InlineInvokeProxyExtensions
{
    public static DateTime GetDateTimeGlobalVar(
        this IInlineInvokeProxy cph,
        string varName,
        bool persisted = true,
        DateTime defaultValue = default
    ) => cph.GetGlobalVar<string>(varName, persisted).FromStreamerBotDateString(defaultValue);

    public static void SetDateTimeGlobalVar(
        this IInlineInvokeProxy cph,
        string varName,
        DateTime? value = null,
        bool persisted = true
    ) => cph.SetGlobalVar(varName, (value ?? DateTime.UtcNow).ToStreamerBotDateString(), persisted);

    public static T GetCustomGlobalVar<T>(
        this IInlineInvokeProxy cph,
        string varName,
        bool persisted = true,
        T defaultValue = default
    ) =>
        cph.GetGlobalVar<string>(varName, persisted) is string stringValue
        && !string.IsNullOrEmpty(stringValue)
            ? JsonConvert.DeserializeObject<T>(stringValue) ?? defaultValue
            : defaultValue;

    public static void SetCustomGlobalVar<T>(
        this IInlineInvokeProxy cph,
        string varName,
        T value,
        bool persisted = true
    ) => cph.SetGlobalVar(varName, JsonConvert.SerializeObject(value), persisted);

    public static void SetCommandState(this IInlineInvokeProxy cph, string commandId, bool state)
    {
        if (state)
            cph.EnableCommand(commandId);
        else
            cph.DisableCommand(commandId);
    }

    public static bool StreamIsLive(this IInlineInvokeProxy cph) =>
        cph.ObsIsConnected() && cph.ObsIsStreaming();

    public static BaseUserInfo[] GetStreamerBotAccounts(this IInlineInvokeProxy cph) =>
        [.. new[] { cph.GetBroadcaster(), cph.GetBot() }.OfType<BaseUserInfo>()];

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

    public static List<BaseUserInfo> BaseUserInfoInGroup(
        this IInlineInvokeProxy cph,
        string groupName
    ) => cph.UsersInGroup(groupName).ConvertAll(user => user.ToBaseUserInfo());

    public static bool CheckGroupMembership(
        this IInlineInvokeProxy cph,
        BaseUserInfo user,
        string groupName
    ) => cph.UserIdInGroup(user.UserId, Platform.Twitch, groupName);

    public static bool EnsureGroupMembershipForUser(
        this IInlineInvokeProxy cph,
        BaseUserInfo user,
        string groupName,
        bool shouldBelongToGroup,
        bool alwaysLog = false
    )
    {
        var displayName = user.Format();
        var groupStatus = shouldBelongToGroup ? "belongs to" : "does not belong to";

        if (alwaysLog)
        {
            Logger.Log($"Ensuring that {displayName} {groupStatus} the \"{groupName}\" group.");
        }

        if (!cph.EnsureGroupState(groupName, true) || user is not { UserId: { } userId })
        {
            return false;
        }

        if (cph.UserIdInGroup(userId, Platform.Twitch, groupName) == shouldBelongToGroup)
        {
            var membershipDescriptor = shouldBelongToGroup ? "a member of" : "not a member of";

            if (alwaysLog)
            {
                Logger.Log(
                    $"{displayName} is already {membershipDescriptor} the \"{groupName}\" group."
                );
            }

            return true;
        }

        var actionVerb = shouldBelongToGroup ? "add" : "remove";
        var success = shouldBelongToGroup
            ? cph.AddUserIdToGroup(userId, Platform.Twitch, groupName)
            : cph.RemoveUserIdFromGroup(userId, Platform.Twitch, groupName);

        if (success)
        {
            if (alwaysLog)
            {
                Logger.Log(
                    $"Successfully {actionVerb}ed {displayName} {(shouldBelongToGroup ? "to" : "from")} the \"{groupName}\" group."
                );
            }

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

    public static List<FileInfo> GetLogFiles(this IInlineInvokeProxy cph, int days = 7)
    {
        var modifiedThreshold = DateTime.UtcNow - TimeSpan.FromDays(days);

        return
        [
            .. Directory
                .GetFiles(".", @"logs\*.log")
                .Select(file => new FileInfo(file))
                .Where(fileInfo => fileInfo.LastWriteTime > modifiedThreshold)
                .OrderByDescending(fileInfo => fileInfo.LastWriteTime),
        ];
    }
}
