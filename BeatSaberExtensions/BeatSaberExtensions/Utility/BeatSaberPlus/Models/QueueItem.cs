using System;
using System.Collections.Generic;
using BeatSaberExtensions.Extensions.BaseUserInfoExtensions;
using BeatSaberExtensions.Extensions.InlineInvokeProxyExtensions;
using BeatSaberExtensions.Utility.Http.BeatSaver.Models;
using Streamer.bot.Plugin.Interface;
using Streamer.bot.Plugin.Interface.Model;

namespace BeatSaberExtensions.Utility.BeatSaberPlus.Models;

public class QueueItem(
    string id,
    string userLogin,
    string songMessage,
    int index,
    Dictionary<string, Beatmap> beatmaps,
    IInlineInvokeProxy cph
)
{
    public string Id { get; } = id;
    public Beatmap Beatmap { get; } = beatmaps.TryGetValue(id, out var beatmap) ? beatmap : null;
    public BaseUserInfo User { get; } = cph.GetUserInfo<BaseUserInfo>(userLogin);
    public int Position { get; } = index + 1;
    public string SongMessage { get; } = songMessage;

    public bool BelongsToUser(BaseUserInfo user) =>
        (SongUser: User, TargetUser: user)
            is { SongUser.UserLogin: { } songUserLogin, TargetUser.UserLogin: { } userLogin }
        && string.Equals(songUserLogin, userLogin, StringComparison.OrdinalIgnoreCase);

    public string ToFriendlyString(bool withPosition, bool withUserName) =>
        string.Concat(
            withPosition ? $"{Position} " : string.Empty,
            withUserName ? $"{User.GetFormattedDisplayName()} " : string.Empty,
            Beatmap is { DisplayString: { } displayString } ? displayString : Id
        );
}
