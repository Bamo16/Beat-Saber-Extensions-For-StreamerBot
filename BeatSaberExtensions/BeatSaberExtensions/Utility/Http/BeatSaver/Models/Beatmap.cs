using System;
using System.Collections.ObjectModel;
using System.Linq;
using BeatSaberExtensions.Utility.Http.BeatLeader.Models;
using Newtonsoft.Json;

namespace BeatSaberExtensions.Utility.Http.BeatSaver.Models;

public class Beatmap
{
    [JsonProperty("id")]
    public string Id { get; private set; }

    [JsonProperty("name")]
    public string Name { get; private set; }

    [JsonProperty("description")]
    public string Description { get; private set; }

    [JsonProperty("metadata")]
    public BeatmapMetadata Metadata { get; private set; }

    [JsonProperty("stats")]
    public BeatmapStats Stats { get; private set; }

    [JsonProperty("tags")]
    public ReadOnlyCollection<string> Tags { get; private set; } = new([]);

    [JsonProperty("updatedAt")]
    public DateTime UpdatedAt { get; private set; }

    [JsonProperty("uploaded")]
    public DateTime Uploaded { get; private set; }

    [JsonProperty("automapper")]
    public bool Automapper { get; private set; }

    [JsonProperty("ranked")]
    public bool Ranked { get; private set; }

    [JsonProperty("qualified")]
    public bool Qualified { get; private set; }

    [JsonProperty("versions")]
    public ReadOnlyCollection<BeatmapVersion> Versions { get; private set; } = new([]);

    [JsonProperty("curatedAt", NullValueHandling = NullValueHandling.Ignore)]
    public DateTime? CuratedAt { get; private set; }

    [JsonProperty("deletedAt")]
    public DateTime DeletedAt { get; private set; }

    private BeatmapVersion _latestVersion;

    [JsonIgnore]
    public BeatmapVersion LatestVersion =>
        _latestVersion ??= Versions
            .OrderByDescending(version => version.CreatedAt)
            .FirstOrDefault();

    [JsonIgnore]
    public string Link => $"https://beatsaver.com/maps/{Id}";

    [JsonIgnore]
    public TimeSpan Duration => TimeSpan.FromSeconds(Metadata.DurationSeconds);

    private readonly DateTime _fetchedAt = DateTime.UtcNow;

    [JsonIgnore]
    public bool ShouldEvict => DateTime.UtcNow > _fetchedAt + UserConfig.BeatmapCacheDuration;

    [JsonIgnore]
    public bool ShouldRefresh =>
        DateTime.UtcNow > _fetchedAt + UserConfig.BeatmapRefreshAfterDuration;

    [JsonIgnore]
    public string DisplayString =>
        ShouldShowSongNameAndAuthor()
            ? $"{(Metadata is { SongAuthorName: { } author } && !string.IsNullOrEmpty(author)
                    ? $"{author} - "
                    : string.Empty)}{(Metadata is { SongName: { } songName } ? songName : string.Empty)}"
            : Id;

    public bool IsMatchingHash(Score score) =>
        score is { Leaderboard.Song.Hash: { } hash }
        && Versions.Any(version => version.Hash.Equals(hash, StringComparison.OrdinalIgnoreCase));

    private bool ShouldShowSongNameAndAuthor() =>
        this
            is {
                Metadata.SongName: not null,
                CuratedAt: var curatedAt,
                UpdatedAt: var updated,
                Stats: { Score: var score, Upvotes: var upvotes },
                Metadata.Duration: var duration
            }
        // Beatmap must meet these requirements to have name/author info displayed
        && (
            // Beatmap is curated
            UserConfig.AlwaysShowWhenCurated && curatedAt is not null
            // - Or -
            // Beatmap wasn't updated in the last 7 days
            || updated.ToUniversalTime() < DateTime.UtcNow.Subtract(UserConfig.MinimumAge)
                // And beatmap score is higher than 60%
                && score >= UserConfig.MinimumScore
                // And beatmap has at least 500 upvotes
                && upvotes >= UserConfig.MinimumUpvotes
                // And beatmap duration is longer than 90 seconds
                && duration >= UserConfig.MinimumDuration
        );
}
