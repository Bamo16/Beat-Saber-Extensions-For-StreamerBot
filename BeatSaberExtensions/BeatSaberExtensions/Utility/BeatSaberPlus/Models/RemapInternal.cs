using Newtonsoft.Json;

namespace BeatSaberExtensions.Utility.BeatSaberPlus.Models;

public class RemapInternal
{
    [JsonProperty("l")]
    public string From { get; set; }

    [JsonProperty("r")]
    public string To { get; set; }
}
