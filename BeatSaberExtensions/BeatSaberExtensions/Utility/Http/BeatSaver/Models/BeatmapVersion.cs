using System;

namespace BeatSaberExtensions.Utility.Http.BeatSaver.Models;

public class BeatmapVersion
{
    public DateTime CreatedAt { get; private set; }
    public string Feedback { get; private set; }
    public short SageScore { get; private set; }
    public string Hash { get; private set; }
    public string Key { get; private set; }
    public DateTime TestplayAt { get; private set; }
    public string CoverUrl { get; private set; }
    public string DownloadUrl { get; private set; }
    public string PreviewUrl { get; private set; }
}
