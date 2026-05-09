using System;
using System.Collections.Generic;
using System.Linq;
using BeatSaberExtensions.Extensions.EnumerableExtensions;
using BeatSaberExtensions.Utility.Http.BeatSaver.Models;

namespace BeatSaberExtensions.Utility.Http.BeatSaver;

#nullable enable

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

    public Beatmap? GetBeatmap(string id) => GetBeatmaps([id]).Values.FirstOrDefault();

    public Dictionary<string, Beatmap> GetBeatmaps(IEnumerable<string> ids)
    {
        lock (_lock)
        {
            EvictExpiredBeatmaps();

            var distinctIds = ids.Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(id => id.ToLower())
                .ToList();

            var idsToFetch = distinctIds
                .Where(id =>
                    !_cachedBeatmaps.TryGetValue(id, out var cached) || cached.ShouldRefresh
                )
                .ToList();

            FetchBeatmaps(idsToFetch);

            // Fall back to whatever is in the cache (possibly stale) when the fetch
            // failed or returned nothing — stale info is far more useful than no info.
            return distinctIds
                .Where(_cachedBeatmaps.ContainsKey)
                .ToDictionary(
                    id => id,
                    id => _cachedBeatmaps[id],
                    StringComparer.OrdinalIgnoreCase
                );
        }
    }

    private void FetchBeatmaps(List<string> idsToFetch)
    {
        if (idsToFetch is not { Count: > 0 })
        {
            return;
        }

        var beatmaps = idsToFetch is { Count: 1 }
            ? SendHttpRequest<Beatmap>(
                relativePath: $"/maps/id/{idsToFetch.Single()}",
                defaultValue: null
            )
                is { Id: not null } singleBeatmap
                ? [singleBeatmap]
                : []
            : GetMultiBeatmapRelativePath(idsToFetch)
                .Select(relativePath =>
                    SendHttpRequest<Dictionary<string, Beatmap>>(
                        relativePath: relativePath,
                        defaultValue: []
                    )
                )
                .OfType<Dictionary<string, Beatmap>>()
                .SelectMany(response =>
                    response.Values.Where(beatmap => beatmap is { Id: not null })
                );

        foreach (var beatmap in beatmaps)
        {
            _cachedBeatmaps[beatmap.Id] = beatmap;
        }
    }

    private void EvictExpiredBeatmaps()
    {
        if (ShouldPerformCacheEviction)
        {
            _cachedBeatmaps
                .Where(kvp => kvp.Value.ShouldEvict)
                .Select(kvp => kvp.Key)
                .ToList()
                .ForEach(id => _cachedBeatmaps.Remove(id));

            _lastCacheEvictionCheck = DateTime.UtcNow;
        }
    }

    private static IEnumerable<string> GetMultiBeatmapRelativePath(IEnumerable<string> ids) =>
        ids.ChunkBy(50).Select(chunk => $"/maps/ids/{string.Join(",", chunk)}");
}
