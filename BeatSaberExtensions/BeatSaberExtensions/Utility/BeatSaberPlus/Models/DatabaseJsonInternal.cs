using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Newtonsoft.Json;

namespace BeatSaberExtensions.Utility.BeatSaberPlus.Models;

public class DatabaseJsonInternal
{
    public ReadOnlyCollection<QueueItem> Queue { get; set; }
    public ReadOnlyCollection<BeatmapInternal> History { get; set; }

    [JsonProperty("blocklist")]
    public ReadOnlyCollection<BeatmapInternal> Blacklist { get; set; }

    [JsonProperty("bannedusers")]
    public ReadOnlyCollection<string> BannedUsers { get; set; }

    [JsonProperty("bannedmappers")]
    public ReadOnlyCollection<string> BannedMappers { get; set; }

    public ReadOnlyCollection<RemapInternal> Remaps { get; set; }

    public IEnumerable<string> BeatmapIds => Queue.Select(item => item.Id);
}
