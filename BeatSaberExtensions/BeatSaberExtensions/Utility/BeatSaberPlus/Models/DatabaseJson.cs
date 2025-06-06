using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace BeatSaberExtensions.Utility.BeatSaberPlus.Models;

public class DatabaseJson(
    IEnumerable<QueueItem> queue,
    IEnumerable<string> history,
    IEnumerable<string> blackList,
    IEnumerable<string> bannedUsers,
    IEnumerable<string> bannedMappers,
    IEnumerable<(string From, string To)> remaps
)
{
    public ReadOnlyCollection<QueueItem> Queue { get; } =
        new ReadOnlyCollection<QueueItem>([.. queue]);
    public IReadOnlyCollection<string> History { get; } =
        history.ToHashSet(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<string> Blacklist { get; } =
        blackList.ToHashSet(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<string> BannedUsers { get; } =
        bannedUsers.ToHashSet(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<string> BannedMappers { get; } =
        bannedMappers.ToHashSet(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, string> Remaps { get; } =
        remaps.ToDictionary(
            remap => remap.From,
            remap => remap.To,
            StringComparer.OrdinalIgnoreCase
        );
}
