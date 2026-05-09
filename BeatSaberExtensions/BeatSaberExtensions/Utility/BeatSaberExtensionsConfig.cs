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

    #region Response Messages

    public string NotConfiguredMessage { get; set; } =
        "The BeatSaber.BeatSaberRoot global variable is not currently configured. Please try running this command again while BeatSaber is running, and the variable will be set automatically.";
    public string QueueEmptyMessage { get; set; } = "There aren't currently any songs in the queue.";
    public string NonModeratorBumpMessage { get; set; } =
        "Only moderators can use the !bsrbump command.🚫";
    public string BlankInputBumpMessage { get; set; } =
        "You must provide either a BSR Id, username, or displayname for the !bsrbump command.🚫";
    public string FailedToGetBeatLeaderIdMessage { get; set; } =
        "Failed to get BeatLeader Id from BeatSaberPlus.";
    public string LookupMissingBsrIdMessage { get; set; } =
        "You must provide a BSR Id with !bsrlookup.";
    public string QueueStatusOpenMessage { get; set; } = "Queue Status: OPEN✅";
    public string QueueStatusClosedMessage { get; set; } = "Queue Status: CLOSED🚫";
    public string StateCommandEnabledMessage { get; set; } = "Enabled Non-mod commands.";
    public string StateCommandDisabledMessage { get; set; } = "Disabled Non-mod commands.";
    public string RaidRequestBumpMessage { get; set; } = "Raid request bump";

    #endregion

    #region Response Format Strings

    public string InvalidInputBumpFormat { get; set; } =
        "The provided value (\"{0}\") does not match any queued BSR Id, username, or displayname.🚫";
    public string NoUserRequestsBumpFormat { get; set; } =
        "There currently aren't any requests in the queue for {0}.";
    public string SongBumpFailureFormat { get; set; } =
        "Couldn't verify song bump success. Please confirm that {0} was bumped to the top.⚠️";
    public string SongMessageFormat { get; set; } = "!songmsg {0} {1} for {2} approved by {3}";
    public string LookupInvalidBsrIdFormat { get; set; } = "Invalid beatmap id: \"{0}\".";
    public string LookupBeatmapNoFoundFormat { get; set; } = "Failed to find beatmap for id: \"{0}\".";
    public string UserHasNoRequestsFormat { get; set; } =
        "{0} {1} not currently have any requests in the queue.";
    public string LookupNoRecentScoresFormat { get; set; } =
        "Didn't find any recent scores by {0} on {1}.";
    public string LookupScoreResultFormat { get; set; } =
        "Beatmap: {0} ({1}) ❙ {2}, played {3}.";
    public string WhenMessageFormat { get; set; } =
        "{0} is at position #{1}, and is playing in {2}.";

    #endregion

    #region General Configuration Settings

    public bool AllowBotWhispers { get; set; } = true;
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
