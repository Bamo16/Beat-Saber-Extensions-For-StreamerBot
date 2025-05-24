using System;
using Newtonsoft.Json;

namespace BeatSaberExtensions.Utility.Http.BeatSaver.Models;

public class BeatmapVersion
{
    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; private set; }

    [JsonProperty("feedback")]
    public string Feedback { get; private set; }

    [JsonProperty("sageScore")]
    public short SageScore { get; private set; }

    [JsonProperty("hash")]
    public string Hash { get; private set; }

    [JsonProperty("key")]
    public string Key { get; private set; }

    [JsonProperty("testplayAt")]
    public DateTime TestplayAt { get; private set; }

    [JsonProperty("coverURL")]
    public string CoverUrl { get; private set; }

    [JsonProperty("downloadURL")]
    public string DownloadUrl { get; private set; }

    [JsonProperty("previewURL")]
    public string PreviewUrl { get; private set; }
}
