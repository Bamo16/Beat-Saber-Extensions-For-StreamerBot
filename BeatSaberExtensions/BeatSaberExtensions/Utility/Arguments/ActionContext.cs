using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using BeatSaberExtensions.Extensions.DictionaryExtensions;
using BeatSaberExtensions.Extensions.InlineInvokeProxyExtensions;
using BeatSaberExtensions.Utility.Logging;
using Streamer.bot.Common.Events;
using Streamer.bot.Plugin.Interface;
using Streamer.bot.Plugin.Interface.Model;

namespace BeatSaberExtensions.Utility.Arguments;

public class ActionContext(Dictionary<string, object> args, IInlineInvokeProxy cph)
{
    private Lazy<string> _commandId;
    private Lazy<string> _command;
    private Lazy<TwitchUserInfo> _caller;
    private Lazy<string> _rawInput;
    private Lazy<string> _input0;

    public Dictionary<string, object> SbArgs { get; } = new Dictionary<string, object>(args);
    public EventType EventType { get; } = cph.GetEventType();

    public TwitchUserInfo Broadcaster => cph.GetBroadcaster();
    public TwitchUserInfo Bot => cph.GetBot();
    public string CommandId => (_commandId ??= new(() => Get<string>("commandId"))).Value;
    public string Command => (_command ??= new(() => Get<string>("command"))).Value;
    public TwitchUserInfo Caller =>
        (_caller ??= new(() => GetUserFromArgs<TwitchUserInfo>())).Value;
    public string RawInput => (_rawInput ??= new(() => Get<string>("rawInput"))).Value;
    public string Input0 => (_input0 ??= new(() => Get<string>("input0"))).Value;

    public T GetUserFromArgs<T>(
        string argName = "user",
        T defaultValue = null,
        params string[] additionalArgNames
    )
        where T : BaseUserInfo =>
        (additionalArgNames ?? [])
            .Prepend(argName)
            .OfType<string>()
            .Select(name => SbArgs.TryGet(name, out string value) ? cph.GetUser<T>(value) : null)
            .OfType<T>()
            .DefaultIfEmpty(defaultValue)
            .FirstOrDefault();

    public T Get<T>(string argName, T defaultValue = default) => SbArgs.Get(argName, defaultValue);

    public bool TryGet<T>(string key, out T value) => SbArgs.TryGet(key, out value);

    public bool IsCaller<T>(T user)
        where T : BaseUserInfo =>
        user is { UserId: { } userId } && Caller is { UserId: { } callerId } && userId == callerId;

    public bool IsBroadcaster<T>(T user)
        where T : BaseUserInfo => user is { UserId: { } userId } && userId == Broadcaster.UserId;

    public T GetUser<T>(string userLogin)
        where T : BaseUserInfo => cph.GetUser<T>(userLogin);

    public void LogArgs(
        string label = "Action started with",
        [CallerMemberName] string methodName = null,
        [CallerLineNumber] int lineNumber = 0
    ) =>
        Logger.LogObject(
            new
            {
                EventType = EventType.ToString(),
                CommandId = CommandId ?? "<null>",
                Command = Command ?? "<null>",
                RawInput = RawInput ?? "<null>",
                Caller = Caller?.UserLogin ?? "<null>",
            },
            label,
            methodName: methodName,
            lineNumber: lineNumber
        );
}
