using System;
using System.Collections.ObjectModel;
using System.Linq;
using BeatSaberExtensions.Extensions.StringExtensions;
using BeatSaberExtensions.Utility.Http.BeatLeader.Models;
using Newtonsoft.Json;

namespace BeatSaberExtensions.Utility.Http.BeatSaver.Models;

public class Beatmap
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public BeatmapMetadata Metadata { get; set; }
    public BeatmapStats Stats { get; set; }
    public ReadOnlyCollection<string> Tags { get; set; } = new([]);
    public DateTime UpdatedAt { get; set; }
    public DateTime Uploaded { get; set; }
    public bool Automapper { get; set; }
    public bool Ranked { get; set; }
    public bool Qualified { get; set; }
    public ReadOnlyCollection<BeatmapVersion> Versions { get; set; } = new([]);
    public DateTime? CuratedAt { get; set; }
    public DateTime DeletedAt { get; set; }

    private BeatmapVersion _latestVersion;

    [JsonIgnore]
    public BeatmapVersion LatestVersion =>
        _latestVersion ??= Versions
            .OrderByDescending(version => version.CreatedAt)
            .FirstOrDefault();

    [JsonIgnore]
    public string Link => Id.GetBeatSaverLink();

    [JsonIgnore]
    public TimeSpan Duration => TimeSpan.FromSeconds(Metadata.DurationSeconds);

    private readonly DateTime _fetchedAt = DateTime.UtcNow;

    [JsonIgnore]
    public bool ShouldRefresh =>
        DateTime.UtcNow > _fetchedAt + UserConfig.Config.BeatmapCacheDuration;

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
            UserConfig.Config.AlwaysShowWhenCurated && curatedAt is not null
            // - Or -
            // Beatmap wasn't updated in the last 7 days
            || updated.ToUniversalTime() < DateTime.UtcNow.Subtract(UserConfig.Config.MinimumAge)
                // And beatmap score is higher than 60%
                && score >= UserConfig.Config.MinimumScore
                // And beatmap has at least 500 upvotes
                && upvotes >= UserConfig.Config.MinimumUpvotes
                // And beatmap duration is longer than 90 seconds
                && duration >= UserConfig.Config.MinimumDuration
        );
}
