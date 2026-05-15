using System;

namespace BeatSaberExtensions.Utility.Http.BeatSaver.Models;

public class BeatmapVersion
{
    public DateTime CreatedAt { get; set; }
    public string Feedback { get; set; }
    public short SageScore { get; set; }
    public string Hash { get; set; }
    public string Key { get; set; }
    public DateTime TestplayAt { get; set; }
    public string CoverUrl { get; set; }
    public string DownloadUrl { get; set; }
    public string PreviewUrl { get; set; }
}
