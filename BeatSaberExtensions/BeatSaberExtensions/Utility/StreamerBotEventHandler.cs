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
            [UserConfig.BumpCommandId] = HandleBumpCommand,
            [UserConfig.LookupCommandId] = HandleLookupCommand,
            [UserConfig.RaidRequestCommandId] = HandleRaidRequest,
            [UserConfig.CaptureBeatLeaderCommandId] = HandleCaptureBeatLeaderId,
            [UserConfig.EnableCommandId] = HandleStateCommand,
            [UserConfig.DisableCommandId] = HandleStateCommand,
            [UserConfig.VersionCommandId] = HandleVersionCommand,
        };

    public void Dispose() => _beatSaberService?.Dispose();

    public bool HandleStreamerBotAction(
        Dictionary<string, object> args,
        bool fromExecuteMethod = false
    )
    {
        var context = Logger.CreateActionContext(args, out var executeSuccess);

        try
        {
            UserConfig.SetConfigValues(context);

            var message = HandleStreamerBotEvent(context, fromExecuteMethod);

            cph.SendMessageAndLog(message);

            executeSuccess = true;
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

    private string HandleStreamerBotEvent(ActionContext context, bool fromExecuteMethod) =>
        fromExecuteMethod
            ? HandleExecuteMethod(context)
            : context switch
            {
                { EventType: EventType.TwitchRaid } => HandleTwitchRaid(context),

                { EventType: EventType.CommandTriggered, CommandId: { } cmdId }
                    when Actions.TryGetValue(cmdId, out var action) => action.Invoke(context),

                { EventType: EventType.CommandTriggered, Command: var cmd, CommandId: var cmdId } =>
                    throw new InvalidOperationException(
                        $"Unsupported command: \"{cmd}\" (\"{cmdId}\")."
                    ),

                { EventType: var eventType } => throw new InvalidOperationException(
                    $"Unsupported EventType: {eventType}."
                ),
            };

    private string HandleExecuteMethod(ActionContext context)
    {
        Logger.Log("Processing custom code event.");

        if (!context.TryGet<string>("user", out _))
        {
            var logMessage = string.Join(
                " ",
                "Received a custom code event trigger, but it did not contain a value for the \"user\" argument.",
                "Please review the configuration of your \"Trigger Custom Event\" sub-action.",
                "Verify that the box for \"Use Args\" is checked."
            );

            Logger.LogError(logMessage);

            throw new ArgumentNullException("user", "The \"user\" argument was missing.");
        }

        var user = context.Caller;

        Logger.Log($"Custom code event redeemer: {user.GetFormattedDisplayName()}.");

        if (_beatSaberService is not { Queue.Count: > 0 })
        {
            return UserConfig.QueueEmptyMessage;
        }

        if (GetQueueItem(user: user) is not { } request)
        {
            return string.Format(
                UserConfig.UserHasNoRequestsFormat,
                user.GetFormattedDisplayName(),
                "does"
            );
        }

        return ProcessSongBump(user, request);
    }

    private string HandleTwitchRaid(ActionContext context)
    {
        if (UserConfig.BumpNextRequestFromRaider is false and var bumpConfig)
        {
            Logger.Log(
                $"Ignoring raid request from {context.Caller.GetFormattedDisplayName()} because {nameof(UserConfig.BumpNextRequestFromRaider)} is {bumpConfig}."
            );

            return null;
        }

        if (_beatSaberService.DatabaseJson.BannedUsers.Contains(context.Caller.UserLogin))
        {
            Logger.Log(
                $"Ignoring raid from {context.Caller.GetFormattedDisplayName()} because user is in bannedusers."
            );

            return null;
        }

        var raider = context.Caller;

        _beatSaberService.EnsureRaidRequestorsMembershipForUser(raider, shouldBelongToGroup: true);

        if (GetQueueItem(user: raider, first: true) is not { } request)
        {
            return null;
        }

        Logger.Log($"Bumping existing request from raider: {raider.GetFormattedDisplayName()}.");

        return ProcessSongBump(
            raider,
            request,
            UserConfig.RaidRequestBumpMessage,
            isRaidRequest: true
        );
    }

    private string HandleQueueCommand(ActionContext context)
    {
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
            .Select(item => item.ToFriendlyString(withPosition: true, withUser: true))
            .Take(max)
            .FormatMultilineChatMessage(header);
    }

    private string HandleMyQueueCommand(ActionContext context)
    {
        if (_beatSaberService is not { Queue: { Count: > 0 } queue })
        {
            return UserConfig.QueueEmptyMessage;
        }

        var user = context.GetUserFromArgs<BaseUserInfo>("input0", defaultValue: context.Caller);
        var isCaller = context.IsCaller(user);
        var userRequests = queue
            .Where(item => item.BelongsToUser(user))
            .Select(item => item.ToFriendlyString(withPosition: true, withUser: false));

        return !userRequests.Any()
            ? string.Format(
                UserConfig.UserHasNoRequestsFormat,
                isCaller ? "You" : user.GetFormattedDisplayName(),
                isCaller ? "do" : "does"
            )
            : userRequests.FormatMultilineChatMessage(
                isCaller ? "Your Requests:" : $"{user.GetFormattedDisplayName()}'s Requests:"
            );
    }

    private string HandleWhenCommand(ActionContext context)
    {
        if (_beatSaberService is not { Queue: { Count: > 0 } queue })
        {
            return UserConfig.QueueEmptyMessage;
        }

        var user = context.GetUserFromArgs<BaseUserInfo>("input0", defaultValue: context.Caller);
        var isCaller = context.IsCaller(user);

        return GetQueueItem(queue, user, first: true) is not { } request
            ? string.Format(
                UserConfig.UserHasNoRequestsFormat,
                isCaller ? "You" : user.GetFormattedDisplayName(),
                isCaller ? "do" : "does"
            )
            : string.Format(
                UserConfig.WhenMessageFormat,
                request.ToFriendlyString(withPosition: false, withUser: false),
                request.Position,
                queue.GetEstimatedWaitTime(request).ToFriendlyString(),
                request is { SongMessage: { } msg } ? $" SongMsg: \"{msg}\"." : string.Empty
            );
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

    private string HandleBumpCommand(ActionContext context)
    {
        Logger.Log("Processing !bsrbump command");

        var approver = context.GetUserFromArgs<TwitchUserInfo>();

        if (approver is not { IsModerator: true })
        {
            Logger.LogWarn(
                $"Non-moderator ({approver.GetFormattedDisplayName()}) attempted !bsrbump."
            );

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
            return ProcessSongBump(queueItemById.User, queueItemById, approver: approver);
        }

        if (context.GetUser<BaseUserInfo>(input0) is not { } user)
        {
            return string.Format(UserConfig.InvalidInputBumpFormat, input0);
        }

        if (GetQueueItem(queue, user: user) is { } queueItemByUser)
        {
            return ProcessSongBump(queueItemByUser.User, queueItemByUser, approver: approver);
        }

        return string.Format(
            UserConfig.UserHasNoRequestsFormat,
            user.GetFormattedDisplayName(),
            "does"
        );
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

        if (context is { Caller: { IsModerator: false } caller })
        {
            Logger.LogWarn(
                $"Ignoring state command because {caller.GetFormattedDisplayName()} is not a moderator."
            );

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

        Logger.Log(
            $"{nameof(HandleRaidRequest)} triggered by raider: {raider.GetFormattedDisplayName()}."
        );

        if (UserConfig.BumpNextRequestFromRaider is false and var bumpConfig)
        {
            Logger.Log(
                $"Ignoring raid request from {raider.GetFormattedDisplayName()} because {nameof(UserConfig.BumpNextRequestFromRaider)} is {bumpConfig}."
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
                $"Failed to handle raid request for {raider.GetFormattedDisplayName()} because the BSR Id could not be parsed from rawInput: \"{context.Get<string>("rawInput")}\"."
            );
        }

        if (!_beatSaberService.UserIsInRaidRequestors(raider))
        {
            throw new InvalidOperationException(
                $"Failed to handle raid request for {raider.GetFormattedDisplayName()} because they are not in the raid requestors group."
            );
        }

        if (!_beatSaberService.ValidateBeatmapId(bsrId))
        {
            Logger.Log(
                $"{raider.GetFormattedDisplayName()} attempted to make a raid request using an invalid BSR Id (\"{bsrId}\"). Taking no action."
            );

            return null;
        }

        var existing = GetQueueItem(id: bsrId);

        if (existing is { User: { UserId: { } userId } requestor } && userId != raider.UserId)
        {
            var logMessage = string.Format(
                "{0} attempted to make a raid request but the BSR Id (\"{1}\") is already in the queue for {{2}}. Taking no action.",
                raider.GetFormattedDisplayName(),
                bsrId,
                requestor.GetFormattedDisplayName()
            );

            Logger.Log(logMessage);

            return null;
        }

        var addedByBot = false;

        if (_beatSaberService is { QueueState: false })
        {
            var attMessage = $"!att {bsrId}";
            Logger.Log(
                $"Queue is closed. Attempting to add BSR Id \"{bsrId}\" for {raider.GetFormattedDisplayName()} using \"{attMessage}\"."
            );
            addedByBot = true;

            cph.SendMessageAndLog(attMessage);
        }

        var request = EnsureRequestIsInQueue(raider, bsrId, addedByBot: addedByBot);

        return ProcessSongBump(
            raider,
            request,
            UserConfig.RaidRequestBumpMessage,
            isRaidRequest: true
        );
    }

    private QueueItem EnsureRequestIsInQueue(BaseUserInfo user, string id, bool addedByBot = false)
    {
        for (var attempt = 1; attempt <= UserConfig.BumpValidationAttempts; attempt++)
        {
            Logger.Log(
                $"Attempting to validate that BSR Id \"{id}\" for {user.GetFormattedDisplayName()} was added to the queue (attempt {attempt} of {UserConfig.BumpValidationAttempts})."
            );

            if (GetQueueItem(user: user, id: id, addedByBot: addedByBot) is { } request)
            {
                Logger.Log(
                    $"Successfully identified request: \"{request.ToFriendlyString(withPosition: true, withUser: true)}\"."
                );

                return request;
            }

            var isLastAttempt = attempt == UserConfig.BumpValidationAttempts;
            var logMessage = string.Format(
                "{0} attempt to identify {1}'s request for BSR Id \"{2}\" was unsuccessful. There are currently {3} in the queue. {4} remaining.{5}",
                attempt.ToOrdinal(),
                user.GetFormattedDisplayName(),
                id,
                "request".Pluralize(_beatSaberService.Queue.Count),
                "attempt".Pluralize(UserConfig.BumpValidationAttempts - attempt),
                !isLastAttempt
                    ? $" Waiting {UserConfig.BumpValidationDelayMs.Format()}ms before next check."
                    : string.Empty
            );

            Logger.Log(logMessage, isLastAttempt ? LogAction.Error : LogAction.Warn);

            if (!isLastAttempt)
                cph.Wait(UserConfig.BumpValidationDelayMs);
        }

        throw new InvalidOperationException(
            $"Failed to find {user.GetFormattedDisplayName()}'s request in queue after {"attempt".Pluralize(UserConfig.BumpValidationAttempts)}."
        );
    }

    private string ProcessSongBump(
        BaseUserInfo requestor,
        QueueItem request,
        string bumpMessage = "Song bump",
        BaseUserInfo approver = null,
        bool isRaidRequest = false
    )
    {
        approver ??= cph.GetBroadcaster();
        var bumpInfo =
            $"{bumpMessage} for {requestor.GetFormattedDisplayName()} (BSR Id: {request.Id})";

        Logger.Log(
            $"Attempting to process {bumpInfo} approved by {approver.GetFormattedDisplayName()}."
        );

        if (request is { Position: 1 })
        {
            Logger.Log($"{bumpInfo} is at the top of the queue.");

            return GetSongBumpMessage(requestor, request, bumpMessage, approver, isRaidRequest);
        }

        var mttMessage = $"!mtt {request.Id}";
        Logger.Log($"Attempting to bump {bumpInfo} using \"{mttMessage}\".");
        cph.SendMessageAndLog(mttMessage);

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
                "After {0} attempt, request for {1} {2}. {3} remaining.{4}",
                attempt.ToOrdinal(),
                requestor.GetFormattedDisplayName(),
                refreshedRequest is null
                    ? "was not found in the queue"
                    : $"was found in the queue at position: {refreshedRequest.Position}.",
                "attempt".Pluralize(UserConfig.BumpValidationAttempts - attempt),
                !isLastAttempt
                    ? $" Waiting {UserConfig.BumpValidationDelayMs.Format()}ms before next check."
                    : string.Empty
            );

            Logger.LogWarn(logMessage);

            if (!isLastAttempt)
                cph.Wait(UserConfig.BumpValidationDelayMs);
        }

        Logger.LogError(
            $"Failed to verify {bumpInfo} after {"attempt".Pluralize(UserConfig.BumpValidationAttempts)}."
        );

        return string.Format(
            UserConfig.SongBumpFailureFormat,
            request.ToFriendlyString(withPosition: false, withUser: true)
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
            requestor.GetFormattedDisplayName(),
            (approver ?? cph.TwitchGetBroadcaster()).GetFormattedDisplayName()
        );
    }

    private QueueItem GetQueueItem(
        ReadOnlyCollection<QueueItem> queue = null,
        BaseUserInfo user = null,
        string id = null,
        bool first = false,
        bool addedByBot = false
    )
    {
        if (!addedByBot && user is null && id is null)
        {
            throw new InvalidOperationException(
                $"Either {nameof(user)} or {nameof(id)} must be non-null."
            );
        }

        var targetUser = addedByBot ? cph.GetBot() : user;
        var items = (queue ?? _beatSaberService.Queue)
            .Where(item =>
                (targetUser is null || item.BelongsToUser(targetUser))
                && (id is null || item.Id.Equals(id.Trim(), StringComparison.OrdinalIgnoreCase))
            )
            .ToList();

        return items switch
        {
            not { Count: > 0 } => null,
            { Count: 1 } => items.Single(),
            { Count: > 1 } when first => items.First(),
            { Count: > 1 } => items.Last(),
        };
    }
}
