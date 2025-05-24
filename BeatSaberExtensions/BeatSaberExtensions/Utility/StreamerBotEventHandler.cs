using System;
using System.Collections.Generic;
using System.Linq;
using BeatSaberExtensions.Extensions.BaseUserInfoExtensions;
using BeatSaberExtensions.Extensions.DateTimeExtensions;
using BeatSaberExtensions.Extensions.DictionaryExtensions;
using BeatSaberExtensions.Extensions.EnumerableExtensions;
using BeatSaberExtensions.Extensions.InlineInvokeProxyExtensions;
using BeatSaberExtensions.Extensions.StringExtensions;
using BeatSaberExtensions.Extensions.TimeSpanExtensions;
using BeatSaberExtensions.Utility.BeatSaberPlus.Models;
using BeatSaberExtensions.Utility.Logging;
using Streamer.bot.Common.Events;
using Streamer.bot.Plugin.Interface;
using Streamer.bot.Plugin.Interface.Model;

namespace BeatSaberExtensions.Utility;

public class StreamerBotEventHandler(IInlineInvokeProxy cph, StreamerBotLogger logger) : IDisposable
{
    private const string QueueCommandId = "243fe815-a265-4607-96ad-36a6ec5f055b";
    private const string MyQueueCommandId = "b6a0b8fa-cedf-421d-8912-1fa771090025";
    private const string WhenCommandId = "6b38768d-a84d-4beb-833e-1bc6405b310a";
    private const string BumpCommandId = "720762d9-2da3-47b5-a149-6abb93341ffc";
    private const string LookupCommandId = "b292e07e-59ff-476c-b03c-99ed05605ad0";
    private const string EnableCommandId = "48c2d642-01ef-4351-99c2-bbf6f89fc7b1";
    private const string DisableCommandId = "58726a1b-3421-46e0-9d03-1e975c8d93b0";
    private const string RaidRequestCommandId = "97eac70e-4537-4339-8f36-46b58eed97ca";
    private const string CaptureBeatLeaderCommandId = "f9303f0c-183c-459f-8982-20f8c44da5d5";

    private static readonly string[] _nonModCommandIds =
    [
        QueueCommandId,
        MyQueueCommandId,
        WhenCommandId,
        LookupCommandId,
    ];

    private readonly BeatSaberService _beatSaberService = new(cph, logger);

    public string HandleStreamerBotEvent(EventType eventType, Dictionary<string, object> sbArgs) =>
        eventType switch
        {
            EventType.TwitchRaid => HandleTwitchRaid(sbArgs),

            EventType.CommandTriggered => HandleCommandTriggered(sbArgs),

            _ => throw new InvalidOperationException($"Unsupported EventType: {eventType}."),
        };

    public void Dispose() => _beatSaberService?.Dispose();

    private string HandleTwitchRaid(Dictionary<string, object> sbArgs)
    {
        if (!UserConfig.BumpNextRequestFromRaider)
        {
            return null;
        }

        var user = cph.GetUserInfoFromArgs<BaseUserInfo>(sbArgs);

        if (_beatSaberService.DatabaseJson.BannedUsers.Contains(user.UserLogin))
        {
            return null;
        }

        if (GetQueueItem(user: user, first: true) is { } request)
        {
            return ProcessSongBump(request, out _, bumpMessage: UserConfig.RaidRequestBumpMessage);
        }

        _beatSaberService.EnsureRaidRequestorsMembershipForUser(user, shouldBelongToGroup: true);

        return null;
    }

    private string HandleCommandTriggered(Dictionary<string, object> sbArgs) =>
        sbArgs.GetArgOrDefault<string>("commandId") switch
        {
            // Beat Saber Extensions needs to see the Beat Saber process running once to detect its plugin locations
            _ when !_beatSaberService.IsConfigured => UserConfig.NotConfiguredMessage,

            // [Chat Commands - Beat Saber Extensions] - !bsrqueue
            QueueCommandId => HandleQueueCommand(sbArgs),

            // [Chat Commands - Beat Saber Extensions] - !bsrmyqueue
            MyQueueCommandId => HandleMyQueueCommand(sbArgs),

            // [Chat Commands - Beat Saber Extensions] - !bsrwhen
            WhenCommandId => HandleWhenCommand(sbArgs),

            // [Chat Commands - Beat Saber Extensions] - !bsrlookup
            LookupCommandId => HandleLookupCommand(sbArgs),

            // [Chat Commands - Beat Saber Extensions] - !bsrbump
            BumpCommandId => HandleBumpCommand(sbArgs),

            // [Chat Commands - Beat Saber Extensions] - !bsrenable
            EnableCommandId => HandleStateCommand(true),

            // [Chat Commands - Beat Saber Extensions] - !bsrdisable
            DisableCommandId => HandleStateCommand(false),

            // [Chat Commands - Beat Saber Extensions] - Bump Raid Request
            RaidRequestCommandId => ProcessRaidRequest(sbArgs),

            // [Chat Commands - Beat Saber Extensions] - Capture BeatLeader ID
            CaptureBeatLeaderCommandId => HandleCaptureBeatLeaderId(sbArgs),

            // Failure Case
            var commandId => throw new InvalidOperationException(
                $"Unsupported Command: \"{sbArgs.GetArgOrDefault<string>("command")}\" (CommandId: {commandId})."
            ),
        };

    private string HandleQueueCommand(Dictionary<string, object> sbArgs) =>
        _beatSaberService is not { Queue: { Count: > 0 } queue }
            ? UserConfig.QueueEmptyMessage
            : queue
                .Select(item => item.ToFriendlyString(withPosition: true, withUserName: true))
                .Take(GetQueueDisplayCountOrDefault(sbArgs))
                .Prepend(
                    _beatSaberService.GetQueueState()
                        ? UserConfig.QueueStatusOpenMessage
                        : UserConfig.QueueStatusClosedMessage
                )
                .FormatMultilineChatMessage();

    private string HandleMyQueueCommand(Dictionary<string, object> sbArgs) =>
        _beatSaberService is not { Queue: { Count: > 0 } queue } ? UserConfig.QueueEmptyMessage
        : cph.GetUserInfoFromArgs<BaseUserInfo>(sbArgs, "input0", "userName") is var user
        && queue
            .FindAll(item => item.BelongsToUser(user))
            .ConvertAll(item => item.ToFriendlyString(withPosition: true, withUserName: false))
            is not { Count: > 0 } userRequests
            ? UserConfig.UserHasNoRequestsMessage
        : userRequests
            .Prepend(
                user.IsCaller(sbArgs)
                    ? "Your Requests:"
                    : $"{user.GetFormattedDisplayName()}'s Requests:"
            )
            .FormatMultilineChatMessage();

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
            return UserConfig.UserHasNoRequestsMessage;
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
        logger.LogInfo("Processing !bsrbump command");

        var approver = cph.GetUserInfoFromArgs<TwitchUserInfo>(sbArgs);

        if (approver is { IsModerator: false })
        {
            logger.LogWarn(
                $"Non-moderator ({approver.GetFormattedDisplayName()}) attempted !bsrbump"
            );

            return UserConfig.NonModeratorBumpMessage;
        }

        if (_beatSaberService is not { Queue.Count: > 0 })
            return UserConfig.QueueEmptyMessage;

        if (!sbArgs.TryGetArg("input0", out string input0) || string.IsNullOrEmpty(input0))
            return UserConfig.BlankInputBumpMessage;

        if (GetQueueItem(id: input0) is { } queueItemById)
            return ProcessSongBump(queueItemById, out _, approver: approver);

        if (cph.GetUserInfo<BaseUserInfo>(input0) is not { } user)
            return string.Format(UserConfig.InvalidInputBumpFormat, input0);

        if (GetQueueItem(user: user) is not { } queueItemByUser)
            return string.Format(
                UserConfig.UserHasNoRequestsMessage,
                user.GetFormattedDisplayName()
            );

        return ProcessSongBump(queueItemByUser, out _, approver: approver);
    }

    private string HandleStateCommand(bool state)
    {
        logger.LogInfo($"Processing !bsrenable/!bsrdisable command with State: \"{state}\".");

        foreach (var commandId in _nonModCommandIds)
        {
            if (state)
                cph.EnableCommand(commandId);
            else
                cph.DisableCommand(commandId);
        }

        return state
            ? UserConfig.StateCommandEnabledMessage
            : UserConfig.StateCommandDisabledMessage;
    }

    private string HandleCaptureBeatLeaderId(Dictionary<string, object> sbArgs)
    {
        if (!sbArgs.TryGetArg("BeatLeaderId", out string beatLeaderId))
            throw new InvalidOperationException("Failed to capture BeatLeaderId.");

        _beatSaberService.SetBeatLeaderIdGlobal(beatLeaderId);

        return null;
    }

    private int GetQueueDisplayCountOrDefault(Dictionary<string, object> sbArgs) =>
        sbArgs.TryGetArg("input0", out string input0)
        && int.TryParse(input0, out var maxCount)
        && maxCount > 0
            ? maxCount
            : UserConfig.MaximumQueueItemCount;

    private string ProcessRaidRequest(Dictionary<string, object> sbArgs)
    {
        var user = cph.GetUserInfoFromArgs<BaseUserInfo>(sbArgs);

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
            // Invalid BSR ID. Do nothing.
            return null;
        }

        QueueItem request = null;
        var requestInfo = $"raid request for {user.GetFormattedDisplayName()} (BSR Id: {bsrId})";

        // Use !att when the queue is closed
        if (_beatSaberService.GetQueueState() is false)
        {
            logger.LogInfo($"Adding {requestInfo} using !att.");

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
                logger.LogWarn(
                    $"Failed to find BSR ID: \"{bsrId}\" in the queue after !att command."
                );

                return null;
            }
        }
        else
        {
            for (var attempt = 0; request is null; attempt++)
            {
                request = GetQueueItem(user: user, id: bsrId, first: true);

                if (request is not null)
                {
                    logger.LogInfo($"Identified {requestInfo} after {attempt + 1} attempts.");
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

        var message = ProcessSongBump(request, out var success, UserConfig.RaidRequestBumpMessage);

        if (success)
        {
            _beatSaberService.EnsureRaidRequestorsMembershipForUser(request.User, false);
        }

        return message;
    }

    private string ProcessSongBump(
        QueueItem request,
        out bool success,
        string bumpMessage = "Song bump",
        BaseUserInfo approver = null
    )
    {
        approver ??= cph.TwitchGetBroadcaster();
        var bumpInfo =
            $"{bumpMessage.ToLower()} for {request.User.GetFormattedDisplayName()} (BSR Id: {request.Id})";

        logger.LogInfo(
            $"Attempting to process {bumpInfo} approved by {approver.GetFormattedDisplayName()}."
        );

        if (request is { Position: 1 })
        {
            logger.Log($"{bumpInfo} is already at the top of the queue.");

            success = true;
            return GetBumpSongMessage(request, bumpMessage, approver);
        }

        cph.SendMessage($"!mtt {request.Id}");

        success = false;

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
            logger.Log($"Successfully processed {bumpInfo}.");

            return GetBumpSongMessage(request, UserConfig.RaidRequestBumpMessage);
        }
        else
        {
            logger.LogError($"Failed to verify {bumpInfo}.");

            return string.Format(
                UserConfig.SongBumpFailureFormat,
                request.ToFriendlyString(withPosition: false, withUserName: true)
            );
        }
    }

    private string GetBumpSongMessage(
        QueueItem request,
        string detail = "Bump",
        BaseUserInfo approver = null
    ) =>
        string.Format(
            UserConfig.SongMessageFormat,
            detail,
            request.Id,
            request.User.GetFormattedDisplayName(),
            (approver ?? cph.TwitchGetBroadcaster()).GetFormattedDisplayName()
        );

    private QueueItem GetQueueItem(
        List<QueueItem> queue = null,
        BaseUserInfo user = null,
        string id = null,
        bool first = false
    ) =>
        user is null && id is null
            ? throw new InvalidOperationException(
                $"Either {nameof(user)} or {nameof(id)} must be non-null."
            )
            : (queue ?? _beatSaberService.Queue).FindAll(item =>
                (user is null || item.BelongsToUser(user))
                && (id is null || id.Trim().Equals(item.Id, StringComparison.OrdinalIgnoreCase))
            ) switch
            {
                { Count: 1 } items => items[0],
                { Count: > 1 } items when first => items.First(),
                { Count: > 1 } items => items.Last(),
                _ => null,
            };
}
