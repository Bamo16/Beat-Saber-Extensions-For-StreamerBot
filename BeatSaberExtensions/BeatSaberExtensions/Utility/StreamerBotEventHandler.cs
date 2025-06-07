using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using BeatSaberExtensions.Extensions.BaseUserInfoExtensions;
using BeatSaberExtensions.Extensions.ComparableExtensions;
using BeatSaberExtensions.Extensions.DateTimeExtensions;
using BeatSaberExtensions.Extensions.DictionaryExtensions;
using BeatSaberExtensions.Extensions.EnumerableExtensions;
using BeatSaberExtensions.Extensions.InlineInvokeProxyExtensions;
using BeatSaberExtensions.Extensions.StringExtensions;
using BeatSaberExtensions.Extensions.TimeSpanExtensions;
using BeatSaberExtensions.Utility.BeatSaberPlus.Models;
using BeatSaberExtensions.Utility.Logging;
using Streamer.bot.Plugin.Interface;
using Streamer.bot.Plugin.Interface.Model;

namespace BeatSaberExtensions.Utility;

public class StreamerBotEventHandler(IInlineInvokeProxy cph) : IDisposable
{
    private readonly BeatSaberService _beatSaberService = new BeatSaberService(cph);

    private Dictionary<string, Func<Dictionary<string, object>, string>> _actions;

    public Dictionary<string, Func<Dictionary<string, object>, string>> Actions =>
        _actions ??= new Dictionary<string, Func<Dictionary<string, object>, string>>()
        {
            [UserConfig.QueueCommandId] = HandleQueueCommand,
            [UserConfig.MyQueueCommandId] = HandleMyQueueCommand,
            [UserConfig.WhenCommandId] = HandleWhenCommand,
            [UserConfig.BumpCommandId] = HandleBumpCommand,
            [UserConfig.LookupCommandId] = HandleLookupCommand,
            [UserConfig.RaidRequestCommandId] = HandleRaidRequest,
            [UserConfig.CaptureBeatLeaderCommandId] = HandleCaptureBeatLeaderId,
            [UserConfig.EnableCommandId] = _ => HandleStateCommand(true),
            [UserConfig.DisableCommandId] = _ => HandleStateCommand(false),
            [UserConfig.VersionCommandId] = _ => HandleVersionCommand(),
        };

    public void Dispose() => _beatSaberService?.Dispose();

    public string HandleTwitchRaid(Dictionary<string, object> sbArgs)
    {
        var user = cph.GetUserInfoFromArgs<BaseUserInfo>(sbArgs);

        if (UserConfig.BumpNextRequestFromRaider is false and var bumpConfig)
        {
            Logger.Log(
                $"Ignoring raid request from {user.GetFormattedDisplayName()} because {nameof(UserConfig.BumpNextRequestFromRaider)} is {bumpConfig}."
            );

            return null;
        }

        if (_beatSaberService.DatabaseJson.BannedUsers.Contains(user.UserLogin))
        {
            Logger.Log(
                $"Ignoring raid from {user.GetFormattedDisplayName()} because user is in bannedusers."
            );

            return null;
        }

        if (GetQueueItem(user: user, first: true) is { } request)
        {
            Logger.Log($"Bumping existing request from raider {user.GetFormattedDisplayName()}.");

            TryProcessSongBump(
                request,
                out var message,
                bumpMessage: UserConfig.RaidRequestBumpMessage
            );

            return message;
        }

        Logger.Log($"Adding raider {user.GetFormattedDisplayName()} to Raid Requestors group.");

        _beatSaberService.EnsureRaidRequestorsMembershipForUser(user, shouldBelongToGroup: true);

        return null;
    }

    private string HandleQueueCommand(Dictionary<string, object> sbArgs)
    {
        if (_beatSaberService is not { Queue: { Count: > 0 } queue })
        {
            return UserConfig.QueueEmptyMessage;
        }

        var max = sbArgs
            .GetArgOrDefault("input0", UserConfig.DefaultQueueItemCount)
            .Clamp(1, UserConfig.MaximumQueueItemCount);
        var header = _beatSaberService.GetQueueState()
            ? UserConfig.QueueStatusOpenMessage
            : UserConfig.QueueStatusClosedMessage;

        return queue
            .Select(item => item.ToFriendlyString(withPosition: true, withUserName: true))
            .Take(max)
            .Prepend(header)
            .FormatMultilineChatMessage();
    }

    private string HandleMyQueueCommand(Dictionary<string, object> sbArgs)
    {
        if (_beatSaberService is not { Queue: { Count: > 0 } queue })
        {
            return UserConfig.QueueEmptyMessage;
        }

        var user = cph.GetUserInfoFromArgs<BaseUserInfo>(sbArgs, "input0", "userName");
        var isCaller = user.IsCaller(sbArgs);
        var userRequests = queue
            .Where(item => item.BelongsToUser(user))
            .Select(item => item.ToFriendlyString(withPosition: true, withUserName: false));

        if (!userRequests.Any())
        {
            return string.Format(
                UserConfig.UserHasNoRequestsFormat,
                isCaller ? "You" : user.GetFormattedDisplayName(),
                isCaller ? "do" : "does"
            );
        }

        return userRequests
            .Prepend(isCaller ? "Your Requests:" : $"{user.GetFormattedDisplayName()}'s Requests:")
            .FormatMultilineChatMessage();
    }

    private string HandleWhenCommand(Dictionary<string, object> sbArgs)
    {
        if (_beatSaberService is not { Queue: { Count: > 0 } queue })
        {
            return UserConfig.QueueEmptyMessage;
        }

        var user = cph.GetUserInfoFromArgs<BaseUserInfo>(sbArgs, "input0", "userName");
        var request = GetQueueItem(queue, user, first: true);

        if (request is null)
        {
            var isCaller = user.IsCaller(sbArgs);

            return string.Format(
                UserConfig.UserHasNoRequestsFormat,
                isCaller ? "You" : user.GetFormattedDisplayName(),
                isCaller ? "do" : "does"
            );
        }

        var estimatedWait = queue
            .Take(request.Position - 1)
            .AccumulateDuration(item =>
                item.Beatmap.Metadata.Duration.Add(UserConfig.TimeBetweenSongs)
            )
            .ToFriendlyString();

        return string.Format(
            UserConfig.WhenMessageFormat,
            request.ToFriendlyString(withPosition: false, withUserName: false),
            request.Position,
            estimatedWait,
            request is { SongMessage: { } msg } ? $" SongMsg: \"{msg}\"." : string.Empty
        );
    }

    private string HandleLookupCommand(Dictionary<string, object> sbArgs)
    {
        if (_beatSaberService is not { BeatLeaderId: { } beatLeaderId })
        {
            return UserConfig.FailedToGetBeatLeaderIdMessage;
        }

        if (!sbArgs.TryGetArg("input0", out string input) || string.IsNullOrEmpty(input))
        {
            return UserConfig.LookupMissingBsrIdMessage;
        }

        if (_beatSaberService.GetBeatmapId(input) is not { } id)
        {
            return string.Format(UserConfig.LookupInvalidBsrIdFormat, input);
        }

        if (_beatSaberService.BeatSaverClient.GetBeatmap(id) is not { } beatmap)
        {
            return string.Format(UserConfig.LookupBeatmapNoFoundFormat, id);
        }

        var score = _beatSaberService.BeatLeaderClient.GetBeatLeaderRecentScore(
            beatLeaderId,
            beatmap
        );

        if (score is not { Timestamp: { } timestamp })
        {
            return string.Format(
                UserConfig.LookupNoRecentScoresFormat,
                cph.TwitchGetBroadcaster().GetFormattedDisplayName(),
                beatmap.DisplayString
            );
        }

        return string.Format(
            UserConfig.LookupScoreResultFormat,
            beatmap.DisplayString,
            score.GetDifficultyShortForm(),
            score.GetFormattedScore(),
            timestamp.ToCurrentDateAwareFriendlyFormat()
        );
    }

    private string HandleBumpCommand(Dictionary<string, object> sbArgs)
    {
        Logger.LogInfo("Processing !bsrbump command");

        var approver = cph.GetUserInfoFromArgs<TwitchUserInfo>(sbArgs);

        if (approver is { IsModerator: false })
        {
            Logger.LogWarn(
                $"Non-moderator ({approver.GetFormattedDisplayName()}) attempted !bsrbump"
            );

            return UserConfig.NonModeratorBumpMessage;
        }

        if (_beatSaberService is not { Queue.Count: > 0 })
        {
            return UserConfig.QueueEmptyMessage;
        }

        if (!sbArgs.TryGetArg("input0", out string input0) || string.IsNullOrEmpty(input0))
        {
            return UserConfig.BlankInputBumpMessage;
        }

        string message;

        if (GetQueueItem(id: input0) is { } queueItemById)
        {
            TryProcessSongBump(queueItemById, out message, approver: approver);
            return message;
        }

        if (cph.GetUserInfo<BaseUserInfo>(input0) is not { } user)
        {
            return string.Format(UserConfig.InvalidInputBumpFormat, input0);
        }

        if (GetQueueItem(user: user) is not { } queueItemByUser)
        {
            return string.Format(
                UserConfig.UserHasNoRequestsFormat,
                user.GetFormattedDisplayName(),
                "does"
            );
        }

        TryProcessSongBump(queueItemByUser, out message, approver: approver);
        return message;
    }

    private string HandleStateCommand(bool state)
    {
        Logger.LogInfo($"Processing !bsrenable/!bsrdisable command with State: \"{state}\".");

        foreach (var commandId in UserConfig.NonModCommandIds)
        {
            cph.SetCommandState(commandId, state);
        }

        return state
            ? UserConfig.StateCommandEnabledMessage
            : UserConfig.StateCommandDisabledMessage;
    }

    private string HandleCaptureBeatLeaderId(Dictionary<string, object> sbArgs)
    {
        if (!sbArgs.TryGetArg("BeatLeaderId", out string beatLeaderId))
        {
            throw new InvalidOperationException("Failed to capture BeatLeaderId.");
        }

        _beatSaberService.SetBeatLeaderIdGlobal(beatLeaderId);

        return null;
    }

    private string HandleVersionCommand() =>
        $"Beat Saber Extensions For StreamerBot Version: {UserConfig.Version} {UserConfig.GithubUrl}";

    private string HandleRaidRequest(Dictionary<string, object> sbArgs)
    {
        var user = cph.GetUserInfoFromArgs<BaseUserInfo>(sbArgs);

        if (UserConfig.BumpNextRequestFromRaider is false and var bumpConfig)
        {
            Logger.Log(
                $"Ignoring raid request from {user.GetFormattedDisplayName()} because {nameof(UserConfig.BumpNextRequestFromRaider)} is {bumpConfig}."
            );

            return null;
        }

        if (!sbArgs.TryGetArg("BsrId", out string bsrId))
        {
            throw new InvalidOperationException(
                $"Failed to handle raid request for {user.GetFormattedDisplayName()} because the BSR Id could not be parsed from rawInput: \"{sbArgs.GetArgOrDefault<string>("rawInput")}\"."
            );
        }

        if (!_beatSaberService.UserIsInRaidRequestors(user))
        {
            throw new InvalidOperationException(
                $"Failed to handle raid request for {user.GetFormattedDisplayName()} because they are not in the raid requestors group."
            );
        }

        if (!_beatSaberService.ValidateBeatmapId(bsrId))
        {
            Logger.Log(
                $"{user.GetFormattedDisplayName()} attempted to make a raid request using an invalid BSR Id. Taking no action."
            );
            return null;
        }

        QueueItem request = null;
        var requestInfo = $"raid request for {user.GetFormattedDisplayName()} (BSR Id: {bsrId})";

        // Use !att when the queue is closed
        if (_beatSaberService.GetQueueState() is false)
        {
            Logger.LogInfo($"Adding raid request for raider {requestInfo} using !att.");

            cph.SendMessage($"!att {bsrId}");

            var attSuccess = cph.TryWaitForCondition(
                () => GetQueueItem(user: user, id: bsrId, first: true),
                (item) => item is not null,
                out request,
                timeoutMs: 10000,
                pollingIntervalMs: 500
            );

            if (!attSuccess)
            {
                Logger.LogWarn(
                    $"Failed to find BSR ID: \"{bsrId}\" in the queue after !att command."
                );

                return null;
            }
        }
        else
        {
            Logger.LogInfo($"Adding raid request for raider {requestInfo} using !mtt.");

            for (var attempt = 0; request is null; attempt++)
            {
                request = GetQueueItem(user: user, id: bsrId, first: true);

                if (request is not null)
                {
                    Logger.LogInfo($"Identified {requestInfo} after {attempt + 1} attempts.");
                }
                else
                {
                    if (attempt < UserConfig.BumpValidationAttempts)
                    {
                        cph.Wait(UserConfig.BumpValidationDelayMs);
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"Failed to to find {requestInfo} in queue  after {attempt + 1} attempts."
                        );
                    }
                }
            }
        }

        if (TryProcessSongBump(request, out var message, UserConfig.RaidRequestBumpMessage))
        {
            _beatSaberService.EnsureRaidRequestorsMembershipForUser(request.User, false);
        }

        return message;
    }

    private bool TryProcessSongBump(
        QueueItem request,
        out string message,
        string bumpMessage = "Song bump",
        BaseUserInfo approver = null
    )
    {
        approver ??= cph.TwitchGetBroadcaster();
        var bumpInfo =
            $"{bumpMessage.ToLowerInvariant()} for {request.User.GetFormattedDisplayName()} (BSR Id: {request.Id})";

        Logger.LogInfo(
            $"Attempting to process {bumpInfo} approved by {approver.GetFormattedDisplayName()}."
        );

        if (request is { Position: 1 })
        {
            Logger.Log($"{bumpInfo} is already at the top of the queue.");

            message = GetBumpSongMessage(request, bumpMessage, approver);
            return false;
        }

        cph.SendMessage($"!mtt {request.Id}");

        var success = false;

        for (var attempt = 0; !success && attempt < UserConfig.BumpValidationAttempts; attempt++)
        {
            if (GetQueueItem(id: request.Id) is { Position: 1 })
            {
                success = true;
            }
            else if (attempt < UserConfig.BumpValidationAttempts)
            {
                cph.Wait(UserConfig.BumpValidationDelayMs);
            }
        }

        if (success)
        {
            Logger.Log($"Successfully processed {bumpInfo}.");

            message = GetBumpSongMessage(request, UserConfig.RaidRequestBumpMessage, approver);
            return true;
        }
        else
        {
            Logger.LogError($"Failed to verify {bumpInfo}.");

            message = string.Format(
                UserConfig.SongBumpFailureFormat,
                request.ToFriendlyString(withPosition: false, withUserName: true)
            );
            return false;
        }
    }

    private string GetBumpSongMessage(
        QueueItem request,
        string detail = "Bump",
        BaseUserInfo approver = null
    ) =>
        string.Format(
            UserConfig.SongMessageFormat,
            request.Id,
            detail,
            request.User.GetFormattedDisplayName(),
            (approver ?? cph.TwitchGetBroadcaster()).GetFormattedDisplayName()
        );

    private QueueItem GetQueueItem(
        ReadOnlyCollection<QueueItem> queue = null,
        BaseUserInfo user = null,
        string id = null,
        bool first = false
    )
    {
        if (user is null && id is null)
        {
            throw new InvalidOperationException(
                $"Either {nameof(user)} or {nameof(id)} must be non-null."
            );
        }

        queue ??= _beatSaberService.Queue;

        var matchingItems = (
            user is not null
                ? queue.Where(item => item.BelongsToUser(user))
                : queue.Where(item => item.Id.Equals(id.Trim(), StringComparison.OrdinalIgnoreCase))
        ).ToList();

        return matchingItems switch
        {
            not { Count: > 0 } => null,
            { Count: 1 } items => items.Single(),
            { Count: > 1 } items => first ? items.First() : items.Last(),
        };
    }
}
