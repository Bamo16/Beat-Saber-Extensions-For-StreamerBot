using System;
using Newtonsoft.Json;

namespace BeatSaberExtensions.Utility.Http.BeatSaver.Models;

public class BeatmapMetadata
{
    public double Bpm { get; private set; }
    public int DurationSeconds { get; private set; }
    public string SongName { get; private set; }
    public string SongSubName { get; private set; }
    public string SongAuthorName { get; private set; }
    public string LevelAuthorName { get; private set; }

    [JsonIgnore]
    public TimeSpan Duration => TimeSpan.FromSeconds(DurationSeconds);
}
