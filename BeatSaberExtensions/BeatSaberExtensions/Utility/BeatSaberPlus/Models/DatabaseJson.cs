using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BeatSaberExtensions.Utility.BeatSaberPlus.Models;

public class DatabaseJson
{
    public ReadOnlyCollection<QueueItem> Queue { get; set; }
    public IReadOnlyCollection<string> History { get; set; }
    public IReadOnlyCollection<string> Blacklist { get; set; }
    public IReadOnlyCollection<string> BannedUsers { get; set; }
    public IReadOnlyCollection<string> BannedMappers { get; set; }
    public IReadOnlyDictionary<string, string> Remaps { get; set; }
}
