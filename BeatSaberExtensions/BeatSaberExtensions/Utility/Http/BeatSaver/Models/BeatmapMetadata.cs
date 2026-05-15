using System;
using Newtonsoft.Json;

namespace BeatSaberExtensions.Utility.Http.BeatSaver.Models;

public class BeatmapMetadata
{
    public double Bpm { get; set; }

    [JsonProperty("duration")]
    public int DurationSeconds { get; set; }
    public string SongName { get; set; }
    public string SongSubName { get; set; }
    public string SongAuthorName { get; set; }
    public string LevelAuthorName { get; set; }

    [JsonIgnore]
    public TimeSpan Duration => TimeSpan.FromSeconds(DurationSeconds);
}
