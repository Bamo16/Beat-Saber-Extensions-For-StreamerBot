using System.Collections.Generic;

namespace BeatSaberExtensions.Utility.BeatSaberPlus.Models;

public class DatabaseJson
{
    public List<QueueItem> Queue { get; set; }
    public HashSet<string> History { get; set; }
    public HashSet<string> Blacklist { get; set; }
    public HashSet<string> BannedUsers { get; set; }
    public HashSet<string> BannedMappers { get; set; }
    public Dictionary<string, string> Remaps { get; set; }
}
