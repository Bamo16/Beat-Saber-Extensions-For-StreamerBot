using Newtonsoft.Json;

namespace BeatSaberExtensions.Utility.BeatSaberPlus.Models;

public class BeatmapInternal
{
    [JsonProperty("key")]
    public string Id { get; set; }
}
