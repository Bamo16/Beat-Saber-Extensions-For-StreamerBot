using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using BeatSaberExtensions.Utility.Http.BeatSaver.Models;

namespace BeatSaberExtensions.Utility.BeatSaberPlus.Models;

public class DatabaseJson(
    IInlineInvokeProxy cph,
    DatabaseJsonInternal data,
    Dictionary<string, Beatmap> beatmaps
)
{
    public ReadOnlyCollection<QueueItem> Queue { get; } =
        data
            .Queue.Select((item, index) => item.WithContext(cph, index, beatmaps))
            .ToList()
            .AsReadOnly();
    public IReadOnlyCollection<string> History { get; } =
        data.History.Select(item => item.Id).ToList().AsReadOnly();
    public IReadOnlyCollection<string> Blacklist { get; } =
        data.Blacklist.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<string> BannedUsers { get; } =
        data.BannedUsers.ToHashSet(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<string> BannedMappers { get; } =
        data.BannedMappers.ToHashSet(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, string> Remaps { get; } =
        data
            .Remaps.GroupBy(remap => remap.From)
            .ToDictionary(
                group => group.Key,
                group => group.First().To,
                StringComparer.OrdinalIgnoreCase
            );
}
