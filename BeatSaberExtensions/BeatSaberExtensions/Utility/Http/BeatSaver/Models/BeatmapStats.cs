using Newtonsoft.Json;

namespace BeatSaberExtensions.Utility.Http.BeatSaver.Models;

public class BeatmapStats
{
    [JsonProperty("plays")]
    public int Plays { get; private set; }

    [JsonProperty("downloads")]
    public int Downloads { get; private set; }

    [JsonProperty("upvotes")]
    public int Upvotes { get; private set; }

    [JsonProperty("downvotes")]
    public int Downvotes { get; private set; }

    [JsonProperty("score")]
    public double Score { get; private set; }
}
