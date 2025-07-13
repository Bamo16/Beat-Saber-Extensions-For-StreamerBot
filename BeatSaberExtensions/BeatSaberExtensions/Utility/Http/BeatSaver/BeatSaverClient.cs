using System;
using System.Collections.Generic;
using System.Linq;
using BeatSaberExtensions.Extensions.EnumerableExtensions;
using BeatSaberExtensions.Utility.Http.BeatSaver.Models;

namespace BeatSaberExtensions.Utility.Http.BeatSaver;

public class BeatSaverClient(bool logWhenSuccessful = false)
    : BaseHttpClient(new Uri(BeatSaverBaseUri), logWhenSuccessful)
{
    private const string BeatSaverBaseUri = "https://api.beatsaver.com/";

    private static readonly TimeSpan _cacheEvictionInterval = TimeSpan.FromMinutes(5);

    private readonly object _lock = new object();
    private readonly Dictionary<string, Beatmap> _cachedBeatmaps = new Dictionary<string, Beatmap>(
        StringComparer.OrdinalIgnoreCase
    );

    private DateTime _lastCacheEvictionCheck = DateTime.MinValue;

    private bool ShouldPerformCacheEviction =>
        _lastCacheEvictionCheck + _cacheEvictionInterval <= DateTime.UtcNow;

    public Beatmap GetBeatmap(string id) => GetBeatmaps([id]).Values.DefaultIfEmpty(null).Single();

    public Dictionary<string, Beatmap> GetBeatmaps(IEnumerable<string> ids)
    {
        lock (_lock)
        {
            var (cachedBeatmaps, idsToFetch) = GetCachedBeatmapsAndIdsToFetch(ids);

            return cachedBeatmaps
                .Concat(FetchBeatmaps(idsToFetch))
                .ToDictionary(
                    beatmap => beatmap.Id,
                    beatmap => beatmap,
                    StringComparer.OrdinalIgnoreCase
                );
        }
    }

    private IEnumerable<Beatmap> FetchBeatmaps(List<string> idsToFetch)
    {
        if (idsToFetch is not { Count: > 0 })
        {
            return [];
        }

        var beatmaps = idsToFetch is { Count: 1 }
            ? SendHttpRequest<Beatmap>(
                relativePath: $"/maps/id/{idsToFetch.Single()}",
                defaultValue: null
            )
                is { } singleBeatmap
                ? [singleBeatmap]
                : []
            : GetMultiBeatmapRelativePath(idsToFetch)
                .Select(relativePath =>
                    SendHttpRequest<Dictionary<string, Beatmap>>(
                        relativePath: relativePath,
                        defaultValue: []
                    )
                )
                .SelectMany(response => response.Values);

        foreach (var beatmap in beatmaps)
        {
            _cachedBeatmaps[beatmap.Id] = beatmap;
        }

        return beatmaps;
    }

    private (List<Beatmap> CachedBeatmaps, List<string> IdsToFetch) GetCachedBeatmapsAndIdsToFetch(
        IEnumerable<string> ids
    )
    {
        EvictExpiredBeatmaps();

        var distinctIds = ids.Distinct(StringComparer.OrdinalIgnoreCase).Select(id => id.ToLower());
        var cachedBeatmaps = new List<Beatmap>();
        var idsToFetch = new List<string>();

        foreach (var id in distinctIds)
        {
            if (IsCached(id, out var cached))
                cachedBeatmaps.Add(cached);
            else
                idsToFetch.Add(id);
        }

        return (cachedBeatmaps, idsToFetch);
    }

    private bool IsCached(string id, out Beatmap beatmap) =>
        _cachedBeatmaps.TryGetValue(id, out beatmap) && beatmap is { ShouldRefresh: false };

    private void EvictExpiredBeatmaps()
    {
        if (ShouldPerformCacheEviction)
        {
            _cachedBeatmaps
                .Where(kvp => kvp.Value.ShouldRefresh)
                .Select(kvp => kvp.Key)
                .ToList()
                .ForEach(id => _cachedBeatmaps.Remove(id));

            _lastCacheEvictionCheck = DateTime.UtcNow;
        }
    }

    private static IEnumerable<string> GetMultiBeatmapRelativePath(IEnumerable<string> ids) =>
        ids.ChunkBy(50).Select(chunk => $"/maps/ids/{string.Join(",", chunk)}");
}
