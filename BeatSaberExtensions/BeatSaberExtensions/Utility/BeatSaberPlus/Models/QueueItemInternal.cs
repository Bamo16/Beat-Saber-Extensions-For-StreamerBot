using System.Collections.Generic;
using BeatSaberExtensions.Extensions.BaseUserInfoExtensions;
using BeatSaberExtensions.Extensions.FormattableExtensions;
using BeatSaberExtensions.Extensions.InlineInvokeProxyExtensions;
using BeatSaberExtensions.Extensions.StringExtensions;
using BeatSaberExtensions.Utility.Http.BeatSaver.Models;
using Newtonsoft.Json;
using Streamer.bot.Plugin.Interface;
using Streamer.bot.Plugin.Interface.Model;

namespace BeatSaberExtensions.Utility.BeatSaberPlus.Models;

public class QueueItem
{
    [JsonProperty("key")]
    public string Id { get; set; }

    [JsonProperty("msg")]
    public string SongMessage { get; set; }

    public string Rqn { get; set; }

    private string _userLogin;

    [JsonIgnore]
    public string UserLogin => _userLogin ??= Rqn.TakeUntilWhitespace();

    [JsonIgnore]
    public Beatmap Beatmap { get; set; }

    [JsonIgnore]
    public BaseUserInfo User { get; set; }

    [JsonIgnore]
    public int Position { get; set; }

    public QueueItem WithContext(
        IInlineInvokeProxy cph,
        int index,
        Dictionary<string, Beatmap> beatmaps
    )
    {
        Position = index + 1;
        User = cph.GetUser<BaseUserInfo>(UserLogin);
        Beatmap = beatmaps.TryGetValue(Id, out var beatmap) ? beatmap : default;

        return this;
    }

    public bool BelongsToUser(BaseUserInfo user) =>
        User is { UserId: { } userId }
        && user is { UserId: { } targetUserId }
        && userId == targetUserId;

    public string Format(bool withPosition, bool withUser) =>
        string.Concat(
            withPosition ? $"{Position.ToOrdinal()} " : string.Empty,
            withUser ? $"{User.Format()} " : string.Empty,
            Beatmap is { DisplayString: { } displayString } ? displayString : Id
        );
}
