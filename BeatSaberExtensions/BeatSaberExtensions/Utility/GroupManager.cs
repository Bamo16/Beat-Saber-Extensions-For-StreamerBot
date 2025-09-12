using System;
using System.Collections.Generic;
using BeatSaberExtensions.Extensions.BaseUserInfoExtensions;
using BeatSaberExtensions.Extensions.InlineInvokeProxyExtensions;
using BeatSaberExtensions.Extensions.TimeSpanExtensions;
using BeatSaberExtensions.Utility.Arguments;
using BeatSaberExtensions.Utility.Logging;
using Streamer.bot.Common.Events;
using Streamer.bot.Plugin.Interface;

namespace BeatSaberExtensions.Utility;

public class GroupManager(IInlineInvokeProxy cph)
{
    private static readonly object _clearGroupsLock = new object();
    private static readonly TimeSpan _timeBetweenStreamsThreshold = TimeSpan.FromMinutes(30);

    private DateTime LastLiveTimestamp
    {
        get => cph.GetDateTimeGlobalVar("GroupManager.LastLiveTimestamp");
        set => cph.SetDateTimeGlobalVar("GroupManager.LastLiveTimestamp", value);
    }
    private List<string> GroupsToClearOnNewStream =>
        cph.GetCustomGlobalVar<List<string>>(
            "GroupManager.GroupsToClearOnNewStream",
            defaultValue: [UserConfig.RaidRequestorGroup]
        );
    private bool ShouldClearGroups =>
        DateTime.UtcNow > LastLiveTimestamp.Add(_timeBetweenStreamsThreshold);

    public void InitializeGroups()
    {
        foreach (var group in UserConfig.EnsureGroupsExist)
            cph.EnsureGroupState(group, true);

        cph.ClearUsersFromGroup(UserConfig.StreamerBotUsersGroup);

        foreach (var user in cph.GetStreamerBotAccounts())
            cph.EnsureGroupMembershipForUser(user, UserConfig.StreamerBotUsersGroup, true);
    }

    public bool HandleGroupManagerAction(ActionContext context)
    {
        var executeSuccess = false;

        try
        {
            switch (context.EventType)
            {
                case EventType.StreamerbotStarted
                or EventType.TwitchStreamOnline
                or EventType.TimedAction:
                    SetLastLiveTimestamp(context);
                    break;

                case EventType.CommandTriggered:
                    HandleAddUserToGroupsCommand(context);
                    break;

                case var eventType:
                    throw new InvalidOperationException($"Unsupported EventType: {eventType}.");
            }

            executeSuccess = true;
        }
        catch (Exception ex)
        {
            Logger.HandleException(ex);
            Logger.LogActionCompletion(executeSuccess);
        }

        return executeSuccess;
    }

    private string SetLastLiveTimestamp(ActionContext context)
    {
        if (context is { StreamIsLive: false })
        {
            return null;
        }

        lock (_clearGroupsLock)
        {
            if (ShouldClearGroups)
            {
                var groupsToClear = GroupsToClearOnNewStream;
                var message =
                    $"{nameof(LastLiveTimestamp)} was longer than {_timeBetweenStreamsThreshold.Format()} ago. Clearing users from the following groups: [{string.Join(", ", groupsToClear)}].";

                Logger.LogWarn(message);

                foreach (var group in groupsToClear)
                {
                    cph.EnsureGroupState(group, true);
                    cph.ClearUsersFromGroup(group);
                }
            }

            if (!cph.GetTimerState(UserConfig.RefreshLastLiveTimestampTimerId))
            {
                cph.EnableTimerById(UserConfig.RefreshLastLiveTimestampTimerId);
            }

            var now = DateTime.UtcNow;

            Logger.Log($"Setting {nameof(LastLiveTimestamp)} to {now}.");

            LastLiveTimestamp = now;
        }

        return null;
    }

    private void HandleAddUserToGroupsCommand(ActionContext context)
    {
        cph.EnsureGroupMembershipForUser(
            context.Caller,
            UserConfig.AllKnownUsersGroup,
            shouldBelongToGroup: true
        );

        if (context.Caller.IsLocalized())
        {
            cph.EnsureGroupMembershipForUser(
                context.Caller,
                UserConfig.LocalizedDisplayUsersGroup,
                shouldBelongToGroup: true
            );
        }
    }
}
