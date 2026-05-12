using System;
using System.IO;
using BeatSaberExtensions.Enums;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace BeatSaberExtensions.Utility;

public class BeatSaberExtensionsConfig
{
    public const string FileName = "BeatSaberExtensions.config.json";

    private static readonly JsonSerializerSettings _serializerSettings =
        new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            Converters = { new StringEnumConverter() },
        };

    #region Customizable Messages

    public string QueueStatusOpenMessage { get; set; } = "Queue Status: OPEN✅";
    public string QueueStatusClosedMessage { get; set; } = "Queue Status: CLOSED🚫";

    // Used to build the `!songmsg` command sent to BeatSaberPlus when a request
    // is bumped. Format args: {0}=BSR Id, {1}=detail, {2}=requestor, {3}=approver.
    public string SongMessageFormat { get; set; } = "!songmsg {0} {1} for {2} approved by {3}";

    #endregion

    #region General Configuration Settings

    public bool AllowBotWhispers { get; set; } = true;
    public bool AllowNonModBsrQueueInChat { get; set; } = false;
    public UsernameDisplayMode UsernameDisplayMode { get; set; } = UsernameDisplayMode.UserLoginOnly;
    public int DefaultQueueItemCount { get; set; } = 5;
    public int MaximumQueueItemCount { get; set; } = 10;
    public int BeatmapCacheDurationMinutes { get; set; } = 30;
    public int SecondsBetweenSongs { get; set; } = 90;
    public int KeepRecentErrorCount { get; set; } = 10;

    #endregion

    #region Song Bump Configuration Settings

    public int BumpValidationAttempts { get; set; } = 3;
    public int BumpValidationDelayMs { get; set; } = 4000;
    public bool BumpNextRequestFromRaider { get; set; } = false;

    #endregion

    #region Beatmap Safe Mode Display Options

    public bool AlwaysShowWhenCurated { get; set; } = true;
    public int MinimumAgeDays { get; set; } = 7;
    public double MinimumScore { get; set; } = 0.65;
    public long MinimumUpvotes { get; set; } = 500L;
    public int MinimumDurationSeconds { get; set; } = 90;

    #endregion

    #region Derived TimeSpan Helpers

    [JsonIgnore]
    public TimeSpan BeatmapCacheDuration => TimeSpan.FromMinutes(BeatmapCacheDurationMinutes);

    [JsonIgnore]
    public TimeSpan TimeBetweenSongs => TimeSpan.FromSeconds(SecondsBetweenSongs);

    [JsonIgnore]
    public TimeSpan MinimumAge => TimeSpan.FromDays(MinimumAgeDays);

    [JsonIgnore]
    public TimeSpan MinimumDuration => TimeSpan.FromSeconds(MinimumDurationSeconds);

    #endregion

    public static string ResolveDefaultPath() =>
        Path.Combine(Directory.GetCurrentDirectory(), FileName);

    public static BeatSaberExtensionsConfig Load(string path) =>
        JsonConvert.DeserializeObject<BeatSaberExtensionsConfig>(
            File.ReadAllText(path),
            _serializerSettings
        ) ?? new BeatSaberExtensionsConfig();

    public void Save(string path) =>
        File.WriteAllText(path, JsonConvert.SerializeObject(this, _serializerSettings));
}
