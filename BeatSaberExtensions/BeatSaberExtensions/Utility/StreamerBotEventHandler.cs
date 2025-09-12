using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using BeatSaberExtensions.Enums;
using BeatSaberExtensions.Extensions.BaseUserInfoExtensions;
using BeatSaberExtensions.Extensions.ComparableExtensions;
using BeatSaberExtensions.Extensions.DateTimeExtensions;
using BeatSaberExtensions.Extensions.EnumerableExtensions;
using BeatSaberExtensions.Extensions.FormattableExtensions;
using BeatSaberExtensions.Extensions.InlineInvokeProxyExtensions;
using BeatSaberExtensions.Extensions.StringExtensions;
using BeatSaberExtensions.Extensions.TimeSpanExtensions;
using BeatSaberExtensions.Utility.Arguments;
using BeatSaberExtensions.Utility.BeatSaberPlus.Models;
using BeatSaberExtensions.Utility.Logging;
using Streamer.bot.Common.Events;
using Streamer.bot.Plugin.Interface;
using Streamer.bot.Plugin.Interface.Model;

namespace BeatSaberExtensions.Utility;

public class StreamerBotEventHandler(IInlineInvokeProxy cph) : IDisposable
{
    private readonly BeatSaberService _beatSaberService = new BeatSaberService(cph);

    private Dictionary<string, Func<ActionContext, string>> _actions;

    private Dictionary<string, Func<ActionContext, string>> Actions =>
        _actions ??= new Dictionary<string, Func<ActionContext, string>>()
        {
            [UserConfig.QueueCommandId] = HandleQueueCommand,
            [UserConfig.MyQueueCommandId] = HandleMyQueueCommand,
            [UserConfig.WhenCommandId] = HandleWhenCommand,
            [UserConfig.LastBeatmapCommandId] = HandleLastBeatmapCommand,
            [UserConfig.BumpCommandId] = HandleBumpCommand,
            [UserConfig.LookupCommandId] = HandleLookupCommand,
            [UserConfig.RaidRequestCommandId] = HandleRaidRequest,
            [UserConfig.LogsCommandId] = (context) => context.HandleLogsCommand(),
            [UserConfig.CaptureBeatLeaderCommandId] = HandleCaptureBeatLeaderId,
            [UserConfig.EnableCommandId] = HandleStateCommand,
            [UserConfig.DisableCommandId] = HandleStateCommand,
            [UserConfig.VersionCommandId] = HandleVersionCommand,
        };

    public void Dispose() => _beatSaberService?.Dispose();

    public bool HandleStreamerBotAction(ActionContext context, bool isCustomBump = false)
    {
        var executeSuccess = false;

        try
        {
            Logger.LogActionStart(context);
            UserConfig.SetConfigValues(context);

            var message = context switch
            {
                _ when isCustomBump => HandleBumpRequestEvent(context),

                { EventType: EventType.TwitchRaid } => HandleTwitchRaid(context),

                { EventType: EventType.CommandTriggered, Command: var cmd, CommandId: { } cmdId } =>
                    Actions.TryGetValue(cmdId, out var action)
                        ? action.Invoke(context)
                        : throw new InvalidOperationException(
                            $"Unsupported command: \"{cmd}\" (\"{cmdId}\")."
                        ),

                { EventType: var eventType } => throw new InvalidOperationException(
                    $"Unsupported EventType: {eventType}."
                ),
            };

            executeSuccess = context.SendResponse(message);
        }
        catch (Exception ex)
        {
            Logger.HandleException(ex);
        }
        finally
        {
            Logger.LogActionCompletion(executeSuccess);
        }

        return executeSuccess;
    }

    private string HandleBumpRequestEvent(ActionContext context)
    {
        if (context is { Caller: { } user })
        {
            Logger.Log($"Processing bump request event for user: {user.Format()}.");

            return _beatSaberService switch
            {
                { Queue: { Count: > 0 } queue } => GetQueueItem(queue, user) is { } request
                    ? ProcessSongBump(context, user, request)
                    : string.Format(UserConfig.UserHasNoRequestsFormat, user.Format(), "does"),

                _ => UserConfig.QueueEmptyMessage,
            };
        }

        Logger.Log("Processing bump request event.");

        var logMessage = string.Join(
            " ",
            "Received an Execute C# Method trigger, but no \"userName\" argument was present.",
            "This argument is normally populated by StreamerBot automatically for any action which is triggered by a Twitch user.",
            "Please review the configuration of your \"Trigger Custom Event\" sub-action.",
            "Verify that the box for \"Use Args\" is checked."
        );

        Logger.LogError(logMessage);

        throw new ArgumentNullException("user", "The \"user\" argument was missing.");
    }

    private string HandleTwitchRaid(ActionContext context)
    {
        if (UserConfig.BumpNextRequestFromRaider is false and var bumpConfig)
        {
            Logger.Log(
                $"Ignoring raid request from {context.Caller.Format()} because {nameof(UserConfig.BumpNextRequestFromRaider)} is {bumpConfig}."
            );

            return null;
        }

        if (_beatSaberService.DatabaseJson.BannedUsers.Contains(context.Caller.UserLogin))
        {
            Logger.Log(
                $"Ignoring raid from {context.Caller.Format()} because user is in bannedusers."
            );

            return null;
        }

        var raider = context.Caller;

        _beatSaberService.EnsureRaidRequestorsMembershipForUser(raider, shouldBelongToGroup: true);

        if (GetQueueItem(user: raider, first: true) is not { } request)
        {
            return null;
        }

        Logger.Log($"Bumping existing request from raider: {raider.Format()}.");

        return ProcessSongBump(
            context,
            raider,
            request,
            UserConfig.RaidRequestBumpMessage,
            isRaidRequest: true
        );
    }

    private string HandleQueueCommand(ActionContext context)
    {
        if (context is { CallerIsMod: false, CommandType: not CommandType.BotWhisper })
        {
            Logger.LogWarn(
                $"Ignoring command: \"{context.Command}\" from {context.Caller.Format()} because they are not a moderator."
            );
            return null;
        }

        if (_beatSaberService is not { Queue: { Count: > 0 } queue })
        {
            return UserConfig.QueueEmptyMessage;
        }

        var max = context
            .Get("input0", UserConfig.DefaultQueueItemCount)
            .Clamp(1, UserConfig.MaximumQueueItemCount);
        var header = _beatSaberService is { QueueState: true }
            ? UserConfig.QueueStatusOpenMessage
            : UserConfig.QueueStatusClosedMessage;

        return queue
            .Select(item => item.Format(withPosition: true, withUser: true))
            .Take(max)
            .FormatMultilineChatMessage(context, header);
    }

    private string HandleMyQueueCommand(ActionContext context)
    {
        if (_beatSaberService is not { Queue: { Count: > 0 } queue })
        {
            return UserConfig.QueueEmptyMessage;
        }

        var user = context.GetUserFromArgs<BaseUserInfo>("input0", "userId");
        var isCaller = context.IsCaller(user);
        var userRequests = queue
            .Where(item => item.BelongsToUser(user))
            .Select(item => item.Format(withPosition: true, withUser: false));

        return !userRequests.Any()
            ? string.Format(
                UserConfig.UserHasNoRequestsFormat,
                isCaller ? "You" : user.Format(),
                isCaller ? "do" : "does"
            )
            : userRequests.FormatMultilineChatMessage(
                context,
                header: isCaller ? "Your Requests:" : $"{user.Format()}'s Requests:"
            );
    }

    private string HandleWhenCommand(ActionContext context)
    {
        if (_beatSaberService is not { Queue: { Count: > 0 } queue })
        {
            return UserConfig.QueueEmptyMessage;
        }

        var user = context.GetUserFromArgs<BaseUserInfo>("input0", "userId");
        var isCaller = context.IsCaller(user);

        return GetQueueItem(queue, user, first: true) is not { } request
            ? string.Format(
                UserConfig.UserHasNoRequestsFormat,
                isCaller ? "You" : user.Format(),
                isCaller ? "do" : "does"
            )
            : string.Format(
                UserConfig.WhenMessageFormat,
                request.Format(withPosition: false, withUser: false),
                request.Position,
                queue.GetEstimatedWaitTime(request).Format(),
                request is { SongMessage: { } msg } ? $" SongMsg: \"{msg}\"." : string.Empty
            );
    }

    private string HandleLastBeatmapCommand(ActionContext context)
    {
        if (_beatSaberService is not { DatabaseJson.History: { Count: > 0 and var count } history })
        {
            return "There are no beatmaps in the history.";
        }

        var index =
            context is { Input0: { } input0 }
            && int.TryParse(input0, out var position)
            && position is >= 1
            && position <= count
                ? position - 1
                : 0;

        return history.ElementAt(index).GetBeatSaverLink();
    }

    private string HandleLookupCommand(ActionContext context)
    {
        if (_beatSaberService is not { BeatLeaderId: { } beatLeaderId })
        {
            return UserConfig.FailedToGetBeatLeaderIdMessage;
        }

        if (!context.TryGet("input0", out string input))
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
                cph.TwitchGetBroadcaster().Format(),
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

    private string HandleBumpCommand(ActionContext context)
    {
        Logger.Log("Processing !bsrbump command");

        var approver = context.GetUserFromArgs<TwitchUserInfo>();

        if (approver is not { IsModerator: true })
        {
            Logger.LogWarn($"Non-moderator ({approver.Format()}) attempted !bsrbump.");

            return UserConfig.NonModeratorBumpMessage;
        }

        if (_beatSaberService.Queue is not { Count: > 0 } queue)
        {
            return UserConfig.QueueEmptyMessage;
        }

        if (context is not { Input0: { } input0 })
        {
            return UserConfig.BlankInputBumpMessage;
        }

        if (GetQueueItem(queue, id: input0) is { } queueItemById)
        {
            return ProcessSongBump(context, queueItemById.User, queueItemById, approver: approver);
        }

        if (cph.GetUser<BaseUserInfo>(input0) is not { } user)
        {
            return string.Format(UserConfig.InvalidInputBumpFormat, input0);
        }

        if (GetQueueItem(queue, user: user) is { } queueItemByUser)
        {
            return ProcessSongBump(
                context,
                queueItemByUser.User,
                queueItemByUser,
                approver: approver
            );
        }

        return string.Format(UserConfig.UserHasNoRequestsFormat, user.Format(), "does");
    }

    private string HandleStateCommand(ActionContext context)
    {
        var state = context switch
        {
            { CommandId: UserConfig.EnableCommandId } => true,
            { CommandId: UserConfig.DisableCommandId } => false,
            { Command: var command, CommandId: var commandId } =>
                throw new InvalidOperationException(
                    $"Unrecognized state command: {command} (\"{commandId}\")."
                ),
        };

        Logger.LogInfo($"Processing !bsrenable/!bsrdisable command with State: \"{state}\".");

        if (context is { CallerTwitch: { IsModerator: false } caller })
        {
            Logger.LogWarn($"Ignoring state command because {caller.Format()} is not a moderator.");

            return null;
        }

        foreach (var commandId in UserConfig.NonModCommandIds)
            cph.SetCommandState(commandId, state);

        return state
            ? UserConfig.StateCommandEnabledMessage
            : UserConfig.StateCommandDisabledMessage;
    }

    private string HandleCaptureBeatLeaderId(ActionContext context)
    {
        if (!context.TryGet("BeatLeaderId", out string beatLeaderId))
        {
            throw new InvalidOperationException("Failed to capture BeatLeaderId.");
        }

        _beatSaberService.SetBeatLeaderIdGlobal(beatLeaderId);

        return null;
    }

    private string HandleVersionCommand(ActionContext _) =>
        $"Beat Saber Extensions For StreamerBot Version: {UserConfig.Version} {UserConfig.GithubUrl}";

    private string HandleRaidRequest(ActionContext context)
    {
        var raider = context.Caller;

        Logger.Log($"{nameof(HandleRaidRequest)} triggered by raider: {raider.Format()}.");

        if (UserConfig.BumpNextRequestFromRaider is false and var bumpConfig)
        {
            Logger.Log(
                $"Ignoring raid request from {raider.Format()} because {nameof(UserConfig.BumpNextRequestFromRaider)} is {bumpConfig}."
            );

            return null;
        }

        // NOTE: While it's usually safe to get named capture groups directly from StreamerBot argument value via:
        //   context.TryGet("BsrId", out string bsrId)
        // there are edge cases where StreamerBot misinterprets valid alphanumeric BSR IDs as numbers scientific notation
        // For example: "!bsr 389e8" results in the "BsrId" argument being parsed as "3.89E+10".
        // To avoid this, we re-match the input manually using the rawInput string.
        if (context.RawInput.MatchBsrRequest() is not { } bsrId)
        {
            throw new InvalidOperationException(
                $"Failed to handle raid request for {raider.Format()} because the BSR Id could not be parsed from rawInput: \"{context.Get<string>("rawInput")}\"."
            );
        }

        if (!cph.CheckGroupMembership(raider, UserConfig.RaidRequestorGroup))
        {
            throw new InvalidOperationException(
                $"Failed to handle raid request for {raider.Format()} because they are not in the raid requestors group."
            );
        }

        if (GetQueueItem(id: bsrId) is { User: { } user } existing && !context.IsCaller(user))
        {
            Logger.Log(
                $"{raider.Format()} attempted to make a raid request but the BSR Id (\"{bsrId}\") is already in the queue for {user.Format()}. Taking no action."
            );

            return $"Could not bump raid request because the requested beatmap ({bsrId}) is already in the queue for {user.Format()}.";
        }

        if (!_beatSaberService.TryValidateBeatmapId(bsrId, out bsrId, out var error))
        {
            Logger.Log(
                $"{raider.Format()} attempted to make a raid request using an invalid BSR Id (\"{bsrId}\"). {error}. Taking no action."
            );

            return null;
        }

        var request = _beatSaberService is { QueueState: false }
            ? AddRequestToTop(context, bsrId, raider, reason: "Queue is closed.")
            : FindRequestInQueue(raider, bsrId)
                ?? AddRequestToTop(
                    context,
                    bsrId,
                    raider,
                    reason: "Failed to find raid request in queue."
                );

        return ProcessSongBump(
            context,
            raider,
            request,
            UserConfig.RaidRequestBumpMessage,
            isRaidRequest: true
        );
    }

    private QueueItem FindRequestInQueue(BaseUserInfo user, string id)
    {
        var maxAttemps = UserConfig.BumpValidationAttempts;

        for (var attempt = 1; attempt <= maxAttemps; attempt++)
        {
            Logger.Log(
                $"Attempting to validate that BSR Id \"{id}\" for {user.Format()} was added to the queue (attempt {attempt} of {maxAttemps})."
            );

            if (GetQueueItem(user: user, id: id) is { } request)
            {
                Logger.Log(
                    $"Successfully identified request: \"{request.Format(withPosition: true, withUser: true)}\"."
                );

                return request;
            }

            var isLastAttempt = attempt == maxAttemps;
            var logMessage = string.Format(
                "{0} attempt to identify {1}'s request for BSR Id \"{2}\" was unsuccessful. There are currently {3} in the queue. {4} remaining.",
                attempt.ToOrdinal(),
                user.Format(),
                id,
                "request".Pluralize(_beatSaberService.Queue.Count),
                "attempt".Pluralize(maxAttemps - attempt)
            );

            Logger.Log(logMessage, isLastAttempt ? LogAction.Error : LogAction.Warn);

            if (!isLastAttempt)
            {
                Logger.Log($"Waiting {maxAttemps.Format()}ms before next check.");
                cph.Wait(UserConfig.BumpValidationDelayMs);
            }
        }

        Logger.LogWarn(
            $"Failed to find {user.Format()}'s request in queue after {"attempt".Pluralize(UserConfig.BumpValidationAttempts)}."
        );

        return default;
    }

    private string ProcessSongBump(
        ActionContext context,
        BaseUserInfo requestor,
        QueueItem request,
        string bumpMessage = "Song bump",
        BaseUserInfo approver = null,
        bool isRaidRequest = false
    )
    {
        approver ??= cph.GetBroadcaster();
        var bumpInfo = $"{bumpMessage} for {requestor.Format()} (BSR Id: {request.Id})";

        Logger.Log($"Attempting to process {bumpInfo} approved by {approver.Format()}.");

        if (request is { Position: 1 })
        {
            Logger.Log($"{bumpInfo} is at the top of the queue.");

            return GetSongBumpMessage(requestor, request, bumpMessage, approver, isRaidRequest);
        }

        var mttMessage = $"!mtt {request.Id}";
        Logger.Log($"Attempting to bump {bumpInfo} using \"{mttMessage}\".");
        context.SendResponse(mttMessage, neverSendAsReply: true, neverSendAsWhisper: true);

        for (var attempt = 1; attempt <= UserConfig.BumpValidationAttempts; attempt++)
        {
            var refreshedRequest = GetQueueItem(user: request.User, id: request.Id);

            if (refreshedRequest is { Position: 1 })
            {
                Logger.Log($"Successfully processed {bumpInfo}.");

                return GetSongBumpMessage(requestor, request, bumpMessage, approver, isRaidRequest);
            }

            var isLastAttempt = attempt == UserConfig.BumpValidationAttempts;
            var logMessage = string.Format(
                "After {0} attempt, request for {1} {2}. {3} remaining.",
                attempt.ToOrdinal(),
                requestor.Format(),
                refreshedRequest is null
                    ? "was not found in the queue"
                    : $"was found in the queue at position: {refreshedRequest.Position}.",
                "attempt".Pluralize(UserConfig.BumpValidationAttempts - attempt)
            );

            Logger.LogWarn(logMessage);

            if (!isLastAttempt)
            {
                Logger.Log(
                    $" Waiting {UserConfig.BumpValidationDelayMs.Format()}ms before next check."
                );
                cph.Wait(UserConfig.BumpValidationDelayMs);
            }
        }

        Logger.LogError(
            $"Failed to verify {bumpInfo} after {"attempt".Pluralize(UserConfig.BumpValidationAttempts)}."
        );

        return string.Format(
            UserConfig.SongBumpFailureFormat,
            request.Format(withPosition: false, withUser: true)
        );
    }

    private string GetSongBumpMessage(
        BaseUserInfo requestor,
        QueueItem request,
        string detail = "Bump",
        BaseUserInfo approver = null,
        bool isRaidRequest = false
    )
    {
        if (isRaidRequest)
        {
            _beatSaberService.EnsureRaidRequestorsMembershipForUser(request.User, false);
        }

        return string.Format(
            UserConfig.SongMessageFormat,
            request.Id,
            detail,
            requestor.Format(),
            (approver ?? cph.TwitchGetBroadcaster()).Format()
        );
    }

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

        var items = (queue ?? _beatSaberService.Queue)
            .Where(item =>
                (user is null || item.BelongsToUser(user))
                && (id is null || item.Id.Equals(id.Trim(), StringComparison.OrdinalIgnoreCase))
            )
            .ToList();

        return items switch
        {
            not { Count: > 0 } => null,
            { Count: 1 } => items[0],
            { Count: > 1 } when first => items[0],
            { Count: > 1 } => items.Last(),
        };
    }

    private QueueItem AddRequestToTop(
        ActionContext context,
        string bsrId,
        BaseUserInfo user,
        string reason = null
    )
    {
        var attMessage = $"!att {bsrId} {user.Format(UsernameDisplayMode.UserLoginOnly)}";
        var logMessage = string.Format(
            "{0}Attempting to add BSR Id \"{1}\" for {2} using \"{3}\".",
            string.IsNullOrEmpty(reason) ? string.Empty : $"{reason} ",
            bsrId,
            user.Format(),
            attMessage
        );
        Logger.Log(logMessage);

        context.SendResponse(attMessage, neverSendAsReply: true, neverSendAsWhisper: true);

        if (FindRequestInQueue(user, bsrId) is not { } request)
        {
            throw new InvalidOperationException(
                $"Failed to find request (BSR Id \"{bsrId}\") for {user.Format()} in the queue after adding with !att.."
            );
        }

        return request;
    }
}
