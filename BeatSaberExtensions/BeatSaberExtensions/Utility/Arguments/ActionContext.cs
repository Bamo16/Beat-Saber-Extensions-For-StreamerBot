using System;
using System.Collections.Generic;
using System.Linq;
using BeatSaberExtensions.Enums;
using BeatSaberExtensions.Extensions.BaseUserInfoExtensions;
using BeatSaberExtensions.Extensions.DictionaryExtensions;
using BeatSaberExtensions.Extensions.InlineInvokeProxyExtensions;
using BeatSaberExtensions.Extensions.StringExtensions;
using BeatSaberExtensions.Utility.Logging;
using Streamer.bot.Common.Events;
using Streamer.bot.Plugin.Interface;
using Streamer.bot.Plugin.Interface.Model;

namespace BeatSaberExtensions.Utility.Arguments;

#nullable enable

public class ActionContext(IInlineInvokeProxy cph, Dictionary<string, object> args)
{
    private const int MaxChatMessageLength = 500;

    public Dictionary<string, object> Args { get; } = new Dictionary<string, object>(args);
    public IInlineInvokeProxy CPH { get; } = cph;
    public EventType EventType { get; } = cph.GetEventType();

    public bool IsTest => Get<bool>("isTest");
    public DateTime QueuedAt => Get("actionQueuedAt", DateTime.UtcNow);
    public CommandType CommandType => Get("commandType", CommandType.NonCommand);
    public string? CommandId => this["commandId"];
    public string? Command => this["command"];
    public string? CommandLower => Command?.ToLowerInvariant();
    public string? Input0 => this["input0"];
    public string? Input1 => this["input1"];
    public string? RawInput => this["rawInput"];
    public string? MsgId => this["msgId"];
    public bool StreamIsLive => CPH.StreamIsLive();

    public string? UserId => this["userId"];
    public string? Username => this["userName"];
    public string? DisplayName => this["user"];
    public TwitchUserInfo Broadcaster => CPH.GetBroadcaster();
    public TwitchUserInfo Bot => CPH.GetBot();
    public BaseUserInfo? Caller => GetUserFromArgs<TwitchUserInfo>();
    public TwitchUserInfo? CallerTwitch => GetUserFromArgs<TwitchUserInfo>();
    public TwitchUserInfoEx? CallerTwitchEx => GetUserFromArgs<TwitchUserInfoEx>();
    public bool CallerIsVip => CallerTwitch is { IsVip: true };
    public bool CallerIsMod => CallerTwitch is { IsModerator: true };
    public bool CallerIsBot => IsBot(Caller);
    public bool CallerIsBroadcaster => IsBroadcaster(Caller);

    public string? this[string argName] => Get<string>(argName);

    public T? Get<T>(string argName, T? defaultValue = default) =>
        TryGet<T>(argName, out var value) ? value : defaultValue;

    public bool TryGet<T>(string argName, out T? value) => Args.TryGet(argName, out value);

    public bool IsCaller<T>(T? user)
        where T : BaseUserInfo =>
        user is { UserId: { } userId } && Caller is { UserId: { } callerId } && userId == callerId;

    public bool IsBroadcaster<T>(T? user)
        where T : BaseUserInfo => user is { UserId: { } userId } && userId == Broadcaster.UserId;

    public bool IsBot<T>(T? user)
        where T : BaseUserInfo => user is { UserId: { } userId } && userId == Bot.UserId;

    public T? GetUserFromArgs<T>(params string[] argNames)
        where T : BaseUserInfo =>
        (argNames ?? [])
            .DefaultIfEmpty("userId")
            .Select(argName =>
                TryGet(argName, out string? value) && !string.IsNullOrWhiteSpace(value)
                    ? CPH.GetUser<T>(
                        value,
                        isUserId: argName.ToLowerInvariant().Contains("id")
                            && value.All(char.IsDigit)
                    )
                    : default
            )
            .OfType<T>()
            .FirstOrDefault();

    public void HandleCompletionResponse(
        bool actionSuccess,
        string? message = null,
        bool alwaysSendMessage = false,
        bool neverSendAsReply = false,
        bool useBot = true,
        bool fallback = true
    )
    {
        if ((!actionSuccess || alwaysSendMessage) && message is { Length: > 0 })
            SendResponse(message, neverSendAsReply, useBot: useBot, fallback: fallback);
    }

    public bool SendResponse(
        string message,
        bool neverSendAsReply = false,
        bool neverSendAsWhisper = false,
        int maxLength = MaxChatMessageLength,
        bool useBot = true,
        bool fallback = true
    )
    {
        var msg = message.Truncate(maxLength);

        if (string.IsNullOrWhiteSpace(msg))
        {
            return true;
        }

        var (type, label) = GetResponsDetail(neverSendAsReply, neverSendAsWhisper);
        Logger.LogSendResponse(msg, label);

        switch (type)
        {
            case ResponseType.BotWhisper
                when Caller is { UserLogin: { } login }
                    && CPH.SendWhisper(login, msg, useBot) is var success:

                if (!success)
                {
                    Logger.LogError($"Failed to send bot message to user: \"{Caller.Format()}\".");
                }

                return success;

            case ResponseType.ChatReply when MsgId is { } msgId:
                CPH.TwitchReplyToMessage(msg, msgId, useBot, fallback);
                return true;

            default:
                CPH.SendMessage(msg, useBot, fallback);
                return true;
        }
    }

    private (ResponseType Type, string Label) GetResponsDetail(
        bool neverSendAsReply = false,
        bool neverSendAsWhisper = false
    ) =>
        this switch
        {
            { IsTest: false, CommandType: CommandType.BotWhisper, Caller: { } caller }
                when !neverSendAsWhisper && UserConfig.Config.AllowBotWhispers => (
                ResponseType.BotWhisper,
                $"Sending bot whisper to {caller.Format()}"
            ),

            { IsTest: false, MsgId: not null } when !neverSendAsReply => (
                ResponseType.ChatReply,
                $"Sending chat reply to {Caller.Format()}"
            ),

            _ => (ResponseType.ChatMessage, "Sending chat message"),
        };
}
