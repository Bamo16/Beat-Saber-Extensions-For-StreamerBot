using BeatSaberExtensions.Extensions.BaseUserInfoExtensions;
using BeatSaberExtensions.Utility.Http.BeatSaver.Models;
using Newtonsoft.Json;
using Streamer.bot.Plugin.Interface.Model;

namespace BeatSaberExtensions.Utility.BeatSaberPlus.Models;

public class QueueItem
{
    [JsonProperty("key")]
    public string Id { get; set; }

    [JsonProperty("rqn")]
    public string UserLogin { get; set; }

    [JsonProperty("msg")]
    public string SongMessage { get; set; }

    public Beatmap Beatmap { get; set; }
    public BaseUserInfo User { get; set; }
    public int Position { get; set; }

    public bool BelongsToUser(BaseUserInfo user) =>
        User is { UserId: { } userId }
        && user is { UserId: { } targetUserId }
        && userId == targetUserId;

    public string ToFriendlyString(bool withPosition, bool withUserName) =>
        string.Concat(
            withPosition ? $"{Position} " : string.Empty,
            withUserName ? $"{User.GetFormattedDisplayName()} " : string.Empty,
            Beatmap is { DisplayString: { } displayString } ? displayString : Id
        );
}
