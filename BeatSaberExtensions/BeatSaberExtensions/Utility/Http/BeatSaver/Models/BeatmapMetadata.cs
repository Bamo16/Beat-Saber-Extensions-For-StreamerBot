using System;
using Newtonsoft.Json;

namespace BeatSaberExtensions.Utility.Http.BeatSaver.Models;

public class BeatmapMetadata
{
    [JsonProperty("bpm")]
    public double Bpm { get; private set; }

    [JsonProperty("duration")]
    public int DurationSeconds { get; private set; }

    [JsonProperty("songName")]
    public string SongName { get; private set; }

    [JsonProperty("songSubName")]
    public string SongSubName { get; private set; }

    [JsonProperty("songAuthorName")]
    public string SongAuthorName { get; private set; }

    [JsonProperty("levelAuthorName")]
    public string LevelAuthorName { get; private set; }

    [JsonIgnore]
    public TimeSpan Duration => TimeSpan.FromSeconds(DurationSeconds);
}
