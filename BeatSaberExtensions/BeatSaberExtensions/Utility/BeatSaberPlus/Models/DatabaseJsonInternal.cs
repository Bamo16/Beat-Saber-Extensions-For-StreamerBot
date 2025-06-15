using System.Collections.ObjectModel;
using Newtonsoft.Json;

namespace BeatSaberExtensions.Utility.BeatSaberPlus.Models;

public class DatabaseJsonInternal
{
    [JsonProperty("queue")]
    public ReadOnlyCollection<QueueItem> Queue { get; set; }

    [JsonProperty("history")]
    public ReadOnlyCollection<BeatmapInternal> History { get; set; }

    [JsonProperty("blacklist")]
    public ReadOnlyCollection<BeatmapInternal> Blacklist { get; set; }

    [JsonProperty("bannedusers")]
    public ReadOnlyCollection<string> BannedUsers { get; set; }

    [JsonProperty("bannedmappers")]
    public ReadOnlyCollection<string> BannedMappers { get; set; }

    [JsonProperty("remaps")]
    public ReadOnlyCollection<RemapInternal> Remaps { get; set; }
}
